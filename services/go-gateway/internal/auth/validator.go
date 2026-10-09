package auth

import (
	"context"
	"crypto/rsa"
	"encoding/base64"
	"encoding/json"
	"errors"
	"fmt"
	"math/big"
	"time"

	"github.com/golang-jwt/jwt/v5"
	"github.com/google/uuid"
	"github.com/redis/go-redis/v9"
)

var (
	ErrKeyNotFound      = errors.New("jwk key not found")
	ErrUnsupportedKey   = errors.New("unsupported key type or algorithm")
	ErrInvalidToken     = errors.New("invalid token signature or format")
	ErrTicketNotFound   = errors.New("websocket ticket not found or already consumed")
	ErrTicketExpired    = errors.New("websocket ticket has expired")
	ErrInvalidUserID    = errors.New("invalid user id in token")
)

type JWK struct {
	Kty string `json:"kty"`
	Use string `json:"use"`
	Alg string `json:"alg"`
	Kid string `json:"kid"`
	N   string `json:"n"`
	E   string `json:"e"`
}

type JWKS struct {
	Keys []JWK `json:"keys"`
}

type TokenClaims struct {
	jwt.RegisteredClaims
	Email string      `json:"email,omitempty"`
	Name  string      `json:"name,omitempty"`
	Role  interface{} `json:"role,omitempty"`
}

type ValidatedUser struct {
	UserID   uuid.UUID
	Email    string
	FullName string
	Roles    []string
}

type TicketPayload struct {
	UserID    uuid.UUID `json:"userId"`
	Roles     []string  `json:"roles"`
	IssuedAt  time.Time `json:"issuedAt"`
	ExpiresAt time.Time `json:"expiresAt"`
}

// ParseJWKS parses JSON Web Key Set from ASP.NET Core /.well-known/jwks.json
func ParseJWKS(jwksData []byte) (*JWKS, error) {
	var jwks JWKS
	if err := json.Unmarshal(jwksData, &jwks); err != nil {
		return nil, fmt.Errorf("failed to unmarshal jwks: %w", err)
	}
	return &jwks, nil
}

// GetPublicKey extracts the RSA public key for a specified kid
func (jwks *JWKS) GetPublicKey(kid string) (*rsa.PublicKey, error) {
	for _, k := range jwks.Keys {
		if kid != "" && k.Kid != kid {
			continue
		}
		if k.Kty != "RSA" {
			continue
		}

		nBytes, err := base64.RawURLEncoding.DecodeString(k.N)
		if err != nil {
			return nil, fmt.Errorf("invalid modulus n: %w", err)
		}

		eBytes, err := base64.RawURLEncoding.DecodeString(k.E)
		if err != nil {
			return nil, fmt.Errorf("invalid exponent e: %w", err)
		}

		var eInt int
		for _, b := range eBytes {
			eInt = (eInt << 8) | int(b)
		}

		return &rsa.PublicKey{
			N: new(big.Int).SetBytes(nBytes),
			E: eInt,
		}, nil
	}
	return nil, ErrKeyNotFound
}

// ValidateJWT verifies an ASP.NET RS256 token against JWKS public keys
func ValidateJWT(tokenStr string, jwks *JWKS, expectedIssuer, expectedAudience string) (*ValidatedUser, error) {
	token, err := jwt.ParseWithClaims(tokenStr, &TokenClaims{}, func(t *jwt.Token) (interface{}, error) {
		if _, ok := t.Method.(*jwt.SigningMethodRSA); !ok {
			return nil, fmt.Errorf("unexpected signing method: %v", t.Header["alg"])
		}

		kid, _ := t.Header["kid"].(string)
		pubKey, err := jwks.GetPublicKey(kid)
		if err != nil {
			return nil, err
		}
		return pubKey, nil
	})

	if err != nil {
		return nil, fmt.Errorf("%w: %v", ErrInvalidToken, err)
	}

	claims, ok := token.Claims.(*TokenClaims)
	if !ok || !token.Valid {
		return nil, ErrInvalidToken
	}

	// Verify issuer if expected
	if expectedIssuer != "" && claims.Issuer != expectedIssuer {
		return nil, fmt.Errorf("issuer mismatch: expected %q, got %q", expectedIssuer, claims.Issuer)
	}

	// Verify audience if expected
	if expectedAudience != "" {
		hasAudience := false
		for _, aud := range claims.Audience {
			if aud == expectedAudience {
				hasAudience = true
				break
			}
		}
		if !hasAudience {
			return nil, fmt.Errorf("audience mismatch: expected %q", expectedAudience)
		}
	}

	sub := claims.Subject
	parsedUUID, err := uuid.Parse(sub)
	if err != nil {
		return nil, ErrInvalidUserID
	}

	var roles []string
	switch r := claims.Role.(type) {
	case string:
		roles = append(roles, r)
	case []interface{}:
		for _, item := range r {
			if str, ok := item.(string); ok {
				roles = append(roles, str)
			}
		}
	}

	return &ValidatedUser{
		UserID:   parsedUUID,
		Email:    claims.Email,
		FullName: claims.Name,
		Roles:    roles,
	}, nil
}

// ValidateAndConsumeTicket reads single-use WebSocket ticket from Redis and deletes it
func ValidateAndConsumeTicket(ctx context.Context, rdb *redis.Client, ticket string) (*TicketPayload, error) {
	if ticket == "" {
		return nil, errors.New("empty ticket")
	}

	key := fmt.Sprintf("ws:ticket:%s", ticket)

	// Fetch ticket from Redis
	val, err := rdb.Get(ctx, key).Result()
	if errors.Is(err, redis.Nil) {
		return nil, ErrTicketNotFound
	}
	if err != nil {
		return nil, fmt.Errorf("redis error reading ticket: %w", err)
	}

	var payload TicketPayload
	if err := json.Unmarshal([]byte(val), &payload); err != nil {
		return nil, fmt.Errorf("failed to unmarshal ticket json: %w", err)
	}

	// Verify expiration
	if time.Now().UTC().After(payload.ExpiresAt) {
		_ = rdb.Del(ctx, key).Err()
		return nil, ErrTicketExpired
	}

	// Enforce single-use consumption (delete key)
	if err := rdb.Del(ctx, key).Err(); err != nil {
		return nil, fmt.Errorf("failed to consume ticket: %w", err)
	}

	return &payload, nil
}

