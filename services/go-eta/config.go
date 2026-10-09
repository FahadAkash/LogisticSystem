package main

import (
	"fmt"
	"net/url"
	"os"
)

type Config struct {
	Port             string
	PostgresHost     string
	PostgresPort     string
	PostgresUser     string
	PostgresPassword string
	PostgresDB       string
	PostgresSSLMode  string
	RedisHost        string
	RedisPort        string
	RedisPassword    string
}

func getEnv(key, fallback string) string {
	if val := os.Getenv(key); val != "" {
		return val
	}
	return fallback
}

func LoadConfig() Config {
	return Config{
		Port:             getEnv("PORT", "8084"),
		PostgresHost:     getEnv("POSTGRES_HOST", "localhost"),
		PostgresPort:     getEnv("POSTGRES_PORT", "5432"),
		PostgresUser:     getEnv("POSTGRES_USER", "postgres"),
		PostgresPassword: getEnv("POSTGRES_PASSWORD", "postgres_secure_pass"),
		PostgresDB:       getEnv("POSTGRES_DB", "appdb"),
		PostgresSSLMode:  getEnv("POSTGRES_SSLMODE", "disable"),
		RedisHost:        getEnv("REDIS_HOST", "localhost"),
		RedisPort:        getEnv("REDIS_PORT", "6379"),
		RedisPassword:    getEnv("REDIS_PASSWORD", "redis_secure_pass"),
	}
}

// PostgresDSN constructs connection URL with schema search_path
func (c Config) PostgresDSN(schema string) string {
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
		"port":          c.Port,
		"postgres_host": c.PostgresHost,
		"postgres_port": c.PostgresPort,
		"postgres_user": c.PostgresUser,
		"postgres_db":   c.PostgresDB,
		"redis_host":    c.RedisHost,
		"redis_port":    c.RedisPort,
	}
}

