package config

import (
	"flag"
	"fmt"
	"net"
	"strings"
	"time"
)

// Config holds all parameters for executing benchmarks and stress tests.
type Config struct {
	LANIP           string
	TargetURL       string
	GoIngestURL     string
	GoGatewayURL    string
	GoDispatchURL   string
	GoEtaURL        string
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

// DetectLANIP discovers the primary non-loopback IPv4 network interface on the local machine.
func DetectLANIP() string {
	addrs, err := net.InterfaceAddrs()
	if err == nil {
		// Prefer 192.168.* LAN address
		for _, addr := range addrs {
			if ipnet, ok := addr.(*net.IPNet); ok && !ipnet.IP.IsLoopback() {
				if ip4 := ipnet.IP.To4(); ip4 != nil {
					ipStr := ip4.String()
					if strings.HasPrefix(ipStr, "192.168.") {
						return ipStr
					}
				}
			}
		}
		// Fallback to any non-loopback IPv4 address
		for _, addr := range addrs {
			if ipnet, ok := addr.(*net.IPNet); ok && !ipnet.IP.IsLoopback() {
				if ip4 := ipnet.IP.To4(); ip4 != nil {
					ipStr := ip4.String()
					if !strings.HasPrefix(ipStr, "169.254.") {
						return ipStr
					}
				}
			}
		}
	}
	return "127.0.0.1"
}

// ParseFlags parses command line arguments and populates Config with sane defaults.
func ParseFlags() *Config {
	cfg := &Config{}

	cfg.LANIP = DetectLANIP()
	defaultTarget := fmt.Sprintf("http://%s:5229", cfg.LANIP)
	defaultIngest := fmt.Sprintf("http://%s:8081", cfg.LANIP)
	defaultGateway := fmt.Sprintf("ws://%s:8083", cfg.LANIP)
	defaultDispatch := fmt.Sprintf("http://%s:8082", cfg.LANIP)
	defaultEta := fmt.Sprintf("http://%s:8084", cfg.LANIP)

	flag.StringVar(&cfg.TargetURL, "target", defaultTarget, "Base target URL (LAN IP default: "+defaultTarget+")")
	flag.StringVar(&cfg.GoIngestURL, "ingest-url", defaultIngest, "Base URL for Go Ingest service (LAN IP default: "+defaultIngest+")")
	flag.StringVar(&cfg.GoGatewayURL, "gateway-url", defaultGateway, "WebSocket URL for Go Gateway (LAN IP default: "+defaultGateway+")")
	flag.StringVar(&cfg.GoDispatchURL, "dispatch-url", defaultDispatch, "Base URL for Go Dispatch service (LAN IP default: "+defaultDispatch+")")
	flag.StringVar(&cfg.GoEtaURL, "eta-url", defaultEta, "Base URL for Go ETA service (LAN IP default: "+defaultEta+")")
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

	userSet := make(map[string]bool)
	flag.Visit(func(f *flag.Flag) {
		userSet[f.Name] = true
	})

	// Normalize URLs (remove trailing slashes)
	cfg.TargetURL = strings.TrimRight(cfg.TargetURL, "/")
	cfg.GoIngestURL = strings.TrimRight(cfg.GoIngestURL, "/")
	cfg.GoGatewayURL = strings.TrimRight(cfg.GoGatewayURL, "/")
	cfg.GoDispatchURL = strings.TrimRight(cfg.GoDispatchURL, "/")
	cfg.GoEtaURL = strings.TrimRight(cfg.GoEtaURL, "/")

	// If user specified custom -target, auto-align child service URLs unless explicitly provided
	if userSet["target"] {
		isProxy := !strings.Contains(cfg.TargetURL, ":5229") && !strings.Contains(cfg.TargetURL, ":5000")
		if isProxy {
			// Reverse proxy topology (e.g. Nginx on port 80/443 at 192.168.0.113)
			if !userSet["ingest-url"] {
				cfg.GoIngestURL = cfg.TargetURL + "/ingest"
			}
			if !userSet["gateway-url"] {
				wsScheme := "ws://"
				if strings.HasPrefix(cfg.TargetURL, "https://") {
					wsScheme = "wss://"
				}
				cleanHost := strings.TrimPrefix(strings.TrimPrefix(cfg.TargetURL, "https://"), "http://")
				cfg.GoGatewayURL = fmt.Sprintf("%s%s/ws", wsScheme, cleanHost)
			}
			if !userSet["dispatch-url"] {
				cfg.GoDispatchURL = cfg.TargetURL + "/dispatch"
			}
			if !userSet["eta-url"] {
				cfg.GoEtaURL = cfg.TargetURL + "/eta"
			}
		} else {
			// Direct port topology (e.g. standalone dev host)
			cleanHost := strings.TrimPrefix(strings.TrimPrefix(cfg.TargetURL, "https://"), "http://")
			if idx := strings.Index(cleanHost, ":"); idx > 0 {
				cleanHost = cleanHost[:idx]
			}
			if !userSet["ingest-url"] {
				cfg.GoIngestURL = fmt.Sprintf("http://%s:8081", cleanHost)
			}
			if !userSet["gateway-url"] {
				cfg.GoGatewayURL = fmt.Sprintf("ws://%s:8083", cleanHost)
			}
			if !userSet["dispatch-url"] {
				cfg.GoDispatchURL = fmt.Sprintf("http://%s:8082", cleanHost)
			}
			if !userSet["eta-url"] {
				cfg.GoEtaURL = fmt.Sprintf("http://%s:8084", cleanHost)
			}
		}
	}

	return cfg
}
