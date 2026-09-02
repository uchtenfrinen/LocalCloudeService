package api

import (
	"encoding/json"
	"net/http"
	"path/filepath"
	"strings"

	"cloud-storage-backend/internal/storage"
)

type Handler struct {
	store *storage.Storage
}

func NewHandler(store *storage.Storage) *Handler {
	return &Handler{store: store}
}

func (h *Handler) Health(w http.ResponseWriter, r *http.Request) {
	writeJSON(w, http.StatusOK, map[string]string{"status": "ok"})
}

func (h *Handler) List(w http.ResponseWriter, r *http.Request) {
	path := r.URL.Query().Get("path")
	files, err := h.store.List(path)
	if err != nil {
		writeError(w, http.StatusBadRequest, err)
		return
	}
	writeJSON(w, http.StatusOK, map[string]any{"path": path, "items": files})
}

func (h *Handler) Mkdir(w http.ResponseWriter, r *http.Request) {
	var body struct {
		Path string `json:"path"`
	}
	if err := json.NewDecoder(r.Body).Decode(&body); err != nil || body.Path == "" {
		writeError(w, http.StatusBadRequest, errBadRequest("path is required"))
		return
	}
	if err := h.store.Mkdir(body.Path); err != nil {
		writeError(w, http.StatusBadRequest, err)
		return
	}
	writeJSON(w, http.StatusCreated, map[string]string{"created": body.Path})
}

func (h *Handler) Upload(w http.ResponseWriter, r *http.Request) {
	if err := r.ParseMultipartForm(1 << 30); err != nil {
		writeError(w, http.StatusBadRequest, errBadRequest("cannot parse multipart form"))
		return
	}
	path := r.URL.Query().Get("path")
	if path == "" {
		path = "."
	}
	file, header, err := r.FormFile("file")
	if err != nil {
		writeError(w, http.StatusBadRequest, errBadRequest("file field is required"))
		return
	}
	defer file.Close()

	relPath := filepath.Join(path, filepath.Base(header.Filename))
	if err := h.store.Save(relPath, file); err != nil {
		writeError(w, http.StatusInternalServerError, err)
		return
	}
	writeJSON(w, http.StatusCreated, map[string]string{"uploaded": relPath})
}

func (h *Handler) Download(w http.ResponseWriter, r *http.Request) {
	path := r.URL.Query().Get("path")
	if path == "" {
		writeError(w, http.StatusBadRequest, errBadRequest("path query param is required"))
		return
	}
	f, err := h.store.Open(path)
	if err != nil {
		writeError(w, http.StatusNotFound, err)
		return
	}
	defer f.Close()

	info, err := f.Stat()
	if err != nil {
		writeError(w, http.StatusInternalServerError, err)
		return
	}
	if info.IsDir() {
		writeError(w, http.StatusBadRequest, errBadRequest("cannot download a directory"))
		return
	}
	w.Header().Set("Content-Disposition", "attachment; filename=\""+filepath.Base(path)+"\"")
	http.ServeContent(w, r, filepath.Base(path), info.ModTime(), f)
}

func (h *Handler) Delete(w http.ResponseWriter, r *http.Request) {
	path := r.URL.Query().Get("path")
	if path == "" {
		writeError(w, http.StatusBadRequest, errBadRequest("path query param is required"))
		return
	}
	if err := h.store.Delete(path); err != nil {
		writeError(w, http.StatusBadRequest, err)
		return
	}
	writeJSON(w, http.StatusOK, map[string]string{"deleted": path})
}

type apiError struct{ msg string }

func (e *apiError) Error() string { return e.msg }

func errBadRequest(msg string) error { return &apiError{msg} }

func writeJSON(w http.ResponseWriter, status int, data any) {
	w.Header().Set("Content-Type", "application/json")
	w.WriteHeader(status)
	json.NewEncoder(w).Encode(data)
}

func writeError(w http.ResponseWriter, status int, err error) {
	msg := ""
	if err != nil {
		msg = strings.TrimSpace(err.Error())
	}
	writeJSON(w, status, map[string]string{"error": msg})
}
