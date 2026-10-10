package config

import (
	"flag"
	"strings"
	"time"
)

// Config holds all parameters for executing benchmarks and stress tests.
type Config struct {
	TargetURL       string
	GoIngestURL     string
	GoGatewayURL    string
	Mode            string
	VUs             int
	MaxVUs          int
	StepVUs         int
	StepDuration    time.Duration
	Duration        time.Duration
	Timeout         time.Duration
	ReportHTML      string
	ReportMD        string
	AdminEmail      string
	AdminPassword   string
	RateLimitRPS    int
	CleanArtifacts  bool
	Verbose         bool
}

// ParseFlags parses command line arguments and populates Config with sane defaults.
func ParseFlags() *Config {
	cfg := &Config{}

	flag.StringVar(&cfg.TargetURL, "target", "http://localhost:5229", "Base target URL (e.g. http://localhost:5229 or http://192.168.0.104:5229)")
	flag.StringVar(&cfg.GoIngestURL, "ingest-url", "http://localhost:8081", "Base URL for Go Ingest service (direct or proxied)")
	flag.StringVar(&cfg.GoGatewayURL, "gateway-url", "ws://localhost:8083", "WebSocket URL for Go Gateway")
	flag.StringVar(&cfg.Mode, "mode", "smoke", "Test mode: smoke, load, stress, spike, ws, journey, all")
	flag.IntVar(&cfg.VUs, "users", 50, "Number of concurrent Virtual Users (VUs)")
	flag.IntVar(&cfg.MaxVUs, "max-users", 1500, "Maximum concurrent VUs for stepped stress test")
	flag.IntVar(&cfg.StepVUs, "step-users", 100, "Number of VUs to add at each ramp step")
	flag.DurationVar(&cfg.StepDuration, "step-duration", 15*time.Second, "Duration to hold each step in stepped stress test")
	flag.DurationVar(&cfg.Duration, "duration", 30*time.Second, "Total test duration for fixed load tests")
	flag.DurationVar(&cfg.Timeout, "timeout", 10*time.Second, "HTTP and WebSocket request timeout")
	flag.StringVar(&cfg.ReportHTML, "report-html", "load_test_report.html", "Path to save interactive HTML report")
	flag.StringVar(&cfg.ReportMD, "report-md", "STRESS_TEST_REPORT.md", "Path to save Markdown summary report")
	flag.StringVar(&cfg.AdminEmail, "admin-email", "admin@logistic.local", "Admin email for seed operations")
	flag.StringVar(&cfg.AdminPassword, "admin-password", "Admin1234!", "Admin password for seed operations")
	flag.IntVar(&cfg.RateLimitRPS, "rate-limit", 0, "Global rate limit (requests per second), 0 = unbounded")
	flag.BoolVar(&cfg.CleanArtifacts, "clean", true, "Attempt cleanup of test orders and artifacts where possible")
	flag.BoolVar(&cfg.Verbose, "verbose", false, "Print detailed request and error diagnostics")

	flag.Parse()

	// Normalize target URL (remove trailing slash)
	cfg.TargetURL = strings.TrimRight(cfg.TargetURL, "/")
	cfg.GoIngestURL = strings.TrimRight(cfg.GoIngestURL, "/")
	cfg.GoGatewayURL = strings.TrimRight(cfg.GoGatewayURL, "/")

	return cfg
}
