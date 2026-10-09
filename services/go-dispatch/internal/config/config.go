package config

import (
	"bufio"
	"fmt"
	"net/url"
	"os"
	"path/filepath"
	"strings"
)

type Config struct {
	Port                  string
	DatabaseURL           string
	PostgresHost          string
	PostgresPort          string
	PostgresUser          string
	PostgresPassword      string
	PostgresDB            string
	PostgresSSLMode       string
	RedisHost             string
	RedisPort             string
	RedisPassword         string
	KafkaBootstrapServers string
}

func loadDotEnv() {
	dir, err := os.Getwd()
	if err != nil {
		return
	}
	for i := 0; i < 8; i++ {
		envPath := filepath.Join(dir, ".env")
		if f, err := os.Open(envPath); err == nil {
			defer f.Close()
			scanner := bufio.NewScanner(f)
			for scanner.Scan() {
				line := strings.TrimSpace(scanner.Text())
				if line == "" || strings.HasPrefix(line, "#") {
					continue
				}
				parts := strings.SplitN(line, "=", 2)
				if len(parts) == 2 {
					key := strings.TrimSpace(parts[0])
					val := strings.TrimSpace(parts[1])
					if os.Getenv(key) == "" {
						_ = os.Setenv(key, val)
					}
				}
			}
			return
		}
		parent := filepath.Dir(dir)
		if parent == dir {
			break
		}
		dir = parent
	}
}

func getEnv(key, fallback string) string {
	if val := os.Getenv(key); val != "" {
		return val
	}
	return fallback
}

func getFirstEnv(fallback string, keys ...string) string {
	for _, key := range keys {
		if val := os.Getenv(key); val != "" {
			return val
		}
	}
	return fallback
}

func Load(defaultPort string) Config {
	loadDotEnv()
	if defaultPort == "" {
		defaultPort = "8082"
	}
	return Config{
		Port:                  getEnv("PORT", defaultPort),
		DatabaseURL:           getFirstEnv("", "DATABASE_URL"),
		PostgresHost:          getFirstEnv("localhost", "DB_HOST", "POSTGRES_HOST"),
		PostgresPort:          getFirstEnv("5432", "DB_PORT", "POSTGRES_PORT"),
		PostgresUser:          getFirstEnv("postgres", "DB_USER", "POSTGRES_USER"),
		PostgresPassword:      getFirstEnv("", "DB_PASSWORD", "POSTGRES_PASSWORD"),
		PostgresDB:            getFirstEnv("appdb", "DB_NAME", "POSTGRES_DB"),
		PostgresSSLMode:       getFirstEnv("disable", "POSTGRES_SSLMODE"),
		RedisHost:             getFirstEnv("localhost", "REDIS_HOST"),
		RedisPort:             getEnv("REDIS_PORT", "6379"),
		RedisPassword:         getEnv("REDIS_PASSWORD", ""),
		KafkaBootstrapServers: getEnv("KAFKA_BOOTSTRAP_SERVERS", "localhost:9092"),
	}
}

// PostgresDSN constructs connection URL with schema search_path
func (c Config) PostgresDSN(schema string) string {
	if c.DatabaseURL != "" {
		sep := "?"
		if strings.Contains(c.DatabaseURL, "?") {
			sep = "&"
		}
		return fmt.Sprintf("%s%ssearch_path=%s,public", c.DatabaseURL, sep, schema)
	}

	userInfo := url.User(c.PostgresUser)
	if c.PostgresPassword != "" {
		userInfo = url.UserPassword(c.PostgresUser, c.PostgresPassword)
	}

	u := url.URL{
		Scheme: "postgres",
		User:   userInfo,
		Host:   fmt.Sprintf("%s:%s", c.PostgresHost, c.PostgresPort),
		Path:   c.PostgresDB,
	}

	q := u.Query()
	q.Set("sslmode", c.PostgresSSLMode)
	q.Set("search_path", fmt.Sprintf("%s,public", schema))
	u.RawQuery = q.Encode()

	return u.String()
}

// RedisAddr returns the host:port for Redis
func (c Config) RedisAddr() string {
	return fmt.Sprintf("%s:%s", c.RedisHost, c.RedisPort)
}

// SafeSummary returns non-sensitive config values for logging
func (c Config) SafeSummary() map[string]string {
	return map[string]string{
		"port":            c.Port,
		"postgres_host":   c.PostgresHost,
		"postgres_port":   c.PostgresPort,
		"postgres_db":     c.PostgresDB,
		"postgres_user":   c.PostgresUser,
		"redis_host":      c.RedisHost,
		"redis_port":      c.RedisPort,
		"kafka_bootstrap": c.KafkaBootstrapServers,
	}
}
