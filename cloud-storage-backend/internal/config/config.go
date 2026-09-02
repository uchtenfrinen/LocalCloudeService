package config

import (
	"bufio"
	"os"
	"strings"
)

type Config struct {
	Port        string
	StorageRoot string
	AuthToken   string
	BaseURL     string
}

// LoadDotEnv reads a local ".env" file (gitignored) and exports its values into
// the process environment — but only for keys not already set. This keeps real
// secrets out of the repo while allowing `./server` to work without exports.
func LoadDotEnv(path string) {
	f, err := os.Open(path)
	if err != nil {
		return
	}
	defer f.Close()

	sc := bufio.NewScanner(f)
	for sc.Scan() {
		line := strings.TrimSpace(sc.Text())
		if line == "" || strings.HasPrefix(line, "#") {
			continue
		}
		parts := strings.SplitN(line, "=", 2)
		if len(parts) != 2 {
			continue
		}
		key := strings.TrimSpace(parts[0])
		val := strings.Trim(strings.TrimSpace(parts[1]), `"'`)
		if os.Getenv(key) == "" {
			os.Setenv(key, val)
		}
	}
}

func Load() *Config {
	LoadDotEnv(".env")
	port := getEnv("STORAGE_PORT", "8080")
	storageRoot := getEnv("STORAGE_ROOT", "./data")
	authToken := getEnv("STORAGE_TOKEN", "")
	baseURL := getEnv("STORAGE_BASE_URL", "")

	if authToken == "" {
		authToken = "changeme"
	}

	return &Config{
		Port:        port,
		StorageRoot: storageRoot,
		AuthToken:   authToken,
		BaseURL:     baseURL,
	}
}

func getEnv(key, fallback string) string {
	if v := os.Getenv(key); v != "" {
		return v
	}
	return fallback
}
