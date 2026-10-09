package auth

import (
	"crypto/rand"
	"crypto/rsa"
	"encoding/base64"
	"encoding/json"
	"math/big"
	"testing"
	"time"

	"github.com/golang-jwt/jwt/v5"
	"github.com/google/uuid"
)

func generateTestKeyPairAndJWKS(t *testing.T, keyID string) (*rsa.PrivateKey, []byte) {
	t.Helper()
	privKey, err := rsa.GenerateKey(rand.Reader, 2048)
	if err != nil {
		t.Fatalf("failed to generate rsa key: %v", err)
	}

	pubKey := &privKey.PublicKey
	nStr := base64.RawURLEncoding.EncodeToString(pubKey.N.Bytes())

	eBytes := big.NewInt(int64(pubKey.E)).Bytes()
	eStr := base64.RawURLEncoding.EncodeToString(eBytes)

	jwk := JWK{
		Kty: "RSA",
		Use: "sig",
		Alg: "RS256",
		Kid: keyID,
		N:   nStr,
		E:   eStr,
	}

	jwks := JWKS{
		Keys: []JWK{jwk},
	}

	data, err := json.Marshal(jwks)
	if err != nil {
		t.Fatalf("failed to marshal jwks: %v", err)
	}

	return privKey, data
}

func TestValidateJWT_WithAspNetCoreJWKS(t *testing.T) {
	keyID := "logistic-auth-key-1"
	privKey, jwksData := generateTestKeyPairAndJWKS(t, keyID)

	jwks, err := ParseJWKS(jwksData)
	if err != nil {
		t.Fatalf("ParseJWKS failed: %v", err)
	}

	userID := uuid.New()
	email := "alice.customer@example.com"
	fullName := "Alice Customer"
	roles := []string{"Customer"}
	issuer := "LogisticServer"
	audience := "LogisticClients"

	// Create JWT token matching TokenService.cs in ASP.NET Core
	claims := TokenClaims{
		RegisteredClaims: jwt.RegisteredClaims{
			Subject:   userID.String(),
			Issuer:    issuer,
			Audience:  jwt.ClaimStrings{audience},
			ID:        uuid.New().String(),
			ExpiresAt: jwt.NewNumericDate(time.Now().Add(15 * time.Minute)),
			IssuedAt:  jwt.NewNumericDate(time.Now()),
		},
		Email: email,
		Name:  fullName,
		Role:  roles[0], // ASP.NET can serialize single role as string
	}

	token := jwt.NewWithClaims(jwt.SigningMethodRS256, claims)
	token.Header["kid"] = keyID

	tokenStr, err := token.SignedString(privKey)
	if err != nil {
		t.Fatalf("failed to sign token: %v", err)
	}

	// Validate using Go auth package
	user, err := ValidateJWT(tokenStr, jwks, issuer, audience)
	if err != nil {
		t.Fatalf("ValidateJWT failed: %v", err)
	}

	if user.UserID != userID {
		t.Errorf("expected UserID %v, got %v", userID, user.UserID)
	}
	if user.Email != email {
		t.Errorf("expected Email %v, got %v", email, user.Email)
	}
	if user.FullName != fullName {
		t.Errorf("expected FullName %v, got %v", fullName, user.FullName)
	}
	if len(user.Roles) != 1 || user.Roles[0] != "Customer" {
		t.Errorf("expected Roles [Customer], got %v", user.Roles)
	}
}

func TestValidateJWT_MultiRoleSupport(t *testing.T) {
	keyID := "logistic-auth-key-1"
	privKey, jwksData := generateTestKeyPairAndJWKS(t, keyID)

	jwks, err := ParseJWKS(jwksData)
	if err != nil {
		t.Fatalf("ParseJWKS failed: %v", err)
	}

	userID := uuid.New()
	issuer := "LogisticServer"
	audience := "LogisticClients"

	// Multi-role array as produced when user has multiple roles in ASP.NET
	claims := TokenClaims{
		RegisteredClaims: jwt.RegisteredClaims{
			Subject:   userID.String(),
			Issuer:    issuer,
			Audience:  jwt.ClaimStrings{audience},
			ExpiresAt: jwt.NewNumericDate(time.Now().Add(15 * time.Minute)),
		},
		Role: []interface{}{"Courier", "Dispatcher"},
	}

	token := jwt.NewWithClaims(jwt.SigningMethodRS256, claims)
	token.Header["kid"] = keyID
	tokenStr, err := token.SignedString(privKey)
	if err != nil {
		t.Fatalf("failed to sign token: %v", err)
	}

	user, err := ValidateJWT(tokenStr, jwks, issuer, audience)
	if err != nil {
		t.Fatalf("ValidateJWT failed: %v", err)
	}

	if len(user.Roles) != 2 || user.Roles[0] != "Courier" || user.Roles[1] != "Dispatcher" {
		t.Errorf("expected roles [Courier Dispatcher], got %v", user.Roles)
	}
}

func TestValidateJWT_RejectsInvalidSignature(t *testing.T) {
	keyID := "logistic-auth-key-1"
	_, jwksData := generateTestKeyPairAndJWKS(t, keyID)
	otherPrivKey, _ := generateTestKeyPairAndJWKS(t, keyID)

	jwks, _ := ParseJWKS(jwksData)

	claims := TokenClaims{
		RegisteredClaims: jwt.RegisteredClaims{
			Subject:   uuid.New().String(),
			Issuer:    "LogisticServer",
			ExpiresAt: jwt.NewNumericDate(time.Now().Add(15 * time.Minute)),
		},
	}

	token := jwt.NewWithClaims(jwt.SigningMethodRS256, claims)
	token.Header["kid"] = keyID
	tamperedToken, _ := token.SignedString(otherPrivKey) // Signed by different key

	_, err := ValidateJWT(tamperedToken, jwks, "LogisticServer", "")
	if err == nil {
		t.Fatal("expected error for token with invalid signature, got nil")
	}
}

func TestValidateJWT_RejectsExpiredToken(t *testing.T) {
	keyID := "logistic-auth-key-1"
	privKey, jwksData := generateTestKeyPairAndJWKS(t, keyID)
	jwks, _ := ParseJWKS(jwksData)

	claims := TokenClaims{
		RegisteredClaims: jwt.RegisteredClaims{
			Subject:   uuid.New().String(),
			Issuer:    "LogisticServer",
			ExpiresAt: jwt.NewNumericDate(time.Now().Add(-5 * time.Minute)), // Expired 5 mins ago
		},
	}

	token := jwt.NewWithClaims(jwt.SigningMethodRS256, claims)
	token.Header["kid"] = keyID
	expiredToken, _ := token.SignedString(privKey)

	_, err := ValidateJWT(expiredToken, jwks, "LogisticServer", "")
	if err == nil {
		t.Fatal("expected error for expired token, got nil")
	}
}

