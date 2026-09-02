package main

import (
	"log"
	"net/http"

	"cloud-storage-backend/internal/api"
	"cloud-storage-backend/internal/auth"
	"cloud-storage-backend/internal/config"
	"cloud-storage-backend/internal/storage"
)

func main() {
	cfg := config.Load()

	if cfg.AuthToken == "changeme" {
		log.Println("WARNING: STORAGE_TOKEN not set — using insecure default 'changeme'. Set it via env or .env.")
	}

	store, err := storage.New(cfg.StorageRoot)
	if err != nil {
		log.Fatalf("storage init failed: %v", err)
	}

	authMiddleware := auth.New(cfg.AuthToken)
	handler := api.NewHandler(store)
	router := api.NewRouter(handler, authMiddleware)

	addr := ":" + cfg.Port
	log.Printf("cloud-storage-backend listening on %s (storage: %s)", addr, cfg.StorageRoot)
	if err := http.ListenAndServe(addr, router); err != nil {
		log.Fatalf("server error: %v", err)
	}
}
