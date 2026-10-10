package client

import (
	"bytes"
	"context"
	"crypto/tls"
	"encoding/json"
	"fmt"
	"io"
	"net"
	"net/http"
	"time"

	"load-tester/pkg/metrics"
)

// TargetClient executes HTTP requests and tracks timing diagnostics.
type TargetClient struct {
	BaseURL    string
	HttpClient *http.Client
}

// NewTargetClient creates an optimized HTTP client tuned for high-concurrency benchmarks.
func NewTargetClient(baseURL string, timeout time.Duration) *TargetClient {
	dialer := &net.Dialer{
		Timeout:   5 * time.Second,
		KeepAlive: 30 * time.Second,
	}

	transport := &http.Transport{
		Proxy:                 http.ProxyFromEnvironment,
		DialContext:           dialer.DialContext,
		MaxIdleConns:          20000,
		MaxIdleConnsPerHost:   5000,
		MaxConnsPerHost:       10000,
		IdleConnTimeout:       90 * time.Second,
		TLSHandshakeTimeout:   5 * time.Second,
		ExpectContinueTimeout: 1 * time.Second,
		TLSClientConfig:       &tls.Config{InsecureSkipVerify: true},
		DisableCompression:   true, // minimize CPU overhead during load tests
		ForceAttemptHTTP2:     true,
	}

	client := &http.Client{
		Transport: transport,
		Timeout:   timeout,
	}

	return &TargetClient{
		BaseURL:    baseURL,
		HttpClient: client,
	}
}

// Do executes an HTTP request, recording duration and returning RequestResult and response body.
func (c *TargetClient) Do(ctx context.Context, method, endpoint string, body []byte, headers map[string]string, token string) (metrics.RequestResult, []byte) {
	url := c.BaseURL + endpoint

	var reqBody io.Reader
	if len(body) > 0 {
		reqBody = bytes.NewReader(body)
	}

	req, err := http.NewRequestWithContext(ctx, method, url, reqBody)
	if err != nil {
		return metrics.RequestResult{
			Endpoint:   endpoint,
			Method:     method,
			StatusCode: 0,
			Duration:   0,
			Err:        err,
			Timestamp:  time.Now(),
		}, nil
	}

	if len(body) > 0 && req.Header.Get("Content-Type") == "" {
		req.Header.Set("Content-Type", "application/json")
	}

	for k, v := range headers {
		req.Header.Set(k, v)
	}

	if token != "" {
		req.Header.Set("Authorization", "Bearer "+token)
	}

	startTime := time.Now()
	resp, err := c.HttpClient.Do(req)
	duration := time.Since(startTime)

	if err != nil {
		return metrics.RequestResult{
			Endpoint:   endpoint,
			Method:     method,
			StatusCode: 0,
			Duration:   duration,
			Err:        err,
			Timestamp:  startTime,
		}, nil
	}
	defer resp.Body.Close()

	respBytes, readErr := io.ReadAll(resp.Body)
	if readErr != nil && err == nil {
		err = readErr
	}

	return metrics.RequestResult{
		Endpoint:   endpoint,
		Method:     method,
		StatusCode: resp.StatusCode,
		Duration:   duration,
		Err:        err,
		BytesRead:  int64(len(respBytes)),
		Timestamp:  startTime,
	}, respBytes
}

// Login performs authentication and parses access token.
func (c *TargetClient) Login(ctx context.Context, email, password string) (string, metrics.RequestResult, error) {
	body, _ := json.Marshal(map[string]string{
		"email":    email,
		"password": password,
	})

	res, data := c.Do(ctx, "POST", "/api/auth/login", body, nil, "")
	if res.StatusCode != http.StatusOK {
		return "", res, fmt.Errorf("login failed with status %d", res.StatusCode)
	}

	var authResp struct {
		AccessToken string `json:"accessToken"`
	}
	if err := json.Unmarshal(data, &authResp); err != nil {
		return "", res, fmt.Errorf("failed to parse login response: %w", err)
	}

	return authResp.AccessToken, res, nil
}
