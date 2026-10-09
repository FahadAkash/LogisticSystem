package config

import (
	"fmt"
	"net/url"
	"os"
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
	if defaultPort == "" {
		defaultPort = "8084"
	}
	return Config{
		Port:                  getEnv("PORT", defaultPort),
		DatabaseURL:           getFirstEnv("postgresql://postgres:postgres_secure_pass@172.19.0.6:5432/appdb", "DATABASE_URL"),
		PostgresHost:          getFirstEnv("172.19.0.6", "DB_HOST", "POSTGRES_HOST"),
		PostgresPort:          getFirstEnv("5432", "DB_PORT", "POSTGRES_PORT"),
		PostgresUser:          getFirstEnv("postgres", "DB_USER", "POSTGRES_USER"),
		PostgresPassword:      getFirstEnv("postgres_secure_pass", "DB_PASSWORD", "POSTGRES_PASSWORD"),
		PostgresDB:            getFirstEnv("appdb", "DB_NAME", "POSTGRES_DB"),
		PostgresSSLMode:       getFirstEnv("disable", "POSTGRES_SSLMODE"),
		RedisHost:             getEnv("REDIS_HOST", "localhost"),
		RedisPort:             getEnv("REDIS_PORT", "6379"),
		RedisPassword:         getEnv("REDIS_PASSWORD", "redis_secure_pass"),
		KafkaBootstrapServers: getEnv("KAFKA_BOOTSTRAP_SERVERS", "127.0.0.1:9094"),
	}
}

// PostgresDSN constructs connection URL with schema search_path
func (c Config) PostgresDSN(schema string) string {
	if c.DatabaseURL != "" {
		sep := "?"
		if strings.Contains(c.DatabaseURL, "?") {
			sep = "&"
		}
		if schema != "" {
			return fmt.Sprintf("%s%ssearch_path=%s", c.DatabaseURL, sep, schema)
		}
		return c.DatabaseURL
	}

	userInfo := url.UserPassword(c.PostgresUser, c.PostgresPassword)
	dsn := fmt.Sprintf("postgres://%s@%s:%s/%s?sslmode=%s",
		userInfo.String(), c.PostgresHost, c.PostgresPort, c.PostgresDB, c.PostgresSSLMode)
	if schema != "" {
		dsn += fmt.Sprintf("&search_path=%s", schema)
	}
	return dsn
}

func (c Config) RedisAddr() string {
	return fmt.Sprintf("%s:%s", c.RedisHost, c.RedisPort)
}

// SafeSummary returns non-sensitive fields for logging without leaking secrets
func (c Config) SafeSummary() map[string]string {
	return map[string]string{
		"port":                    c.Port,
		"postgres_host":           c.PostgresHost,
		"postgres_port":           c.PostgresPort,
		"postgres_user":           c.PostgresUser,
		"postgres_db":             c.PostgresDB,
		"redis_host":              c.RedisHost,
		"redis_port":              c.RedisPort,
		"kafka_bootstrap_servers": c.KafkaBootstrapServers,
	}
}
