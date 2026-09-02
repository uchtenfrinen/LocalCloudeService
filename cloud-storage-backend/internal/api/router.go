package api

import (
	"net/http"

	"cloud-storage-backend/internal/auth"
)

func NewRouter(h *Handler, a *auth.Auth) http.Handler {
	mux := http.NewServeMux()

	mux.HandleFunc("/api/health", h.Health)

	mux.Handle("/api/files", a.Middleware(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		switch r.Method {
		case http.MethodGet:
			h.List(w, r)
		case http.MethodDelete:
			h.Delete(w, r)
		default:
			methodNotAllowed(w)
		}
	})))

	mux.Handle("/api/mkdir", a.Middleware(methodOpt(http.MethodPost, h.Mkdir)))
	mux.Handle("/api/upload", a.Middleware(methodOpt(http.MethodPost, h.Upload)))
	mux.Handle("/api/download", a.Middleware(methodOpt(http.MethodGet, h.Download)))

	return mux
}

// methodNotAllowed rejects a request whose HTTP method is unsupported.
func methodNotAllowed(w http.ResponseWriter) {
	writeError(w, http.StatusMethodNotAllowed, errBadRequest("method not allowed"))
}

// MethodOpt restricts a handler to a single HTTP method, responding with 405
// for anything else.
func methodOpt(method string, next http.HandlerFunc) http.HandlerFunc {
	return func(w http.ResponseWriter, r *http.Request) {
		if r.Method != method {
			methodNotAllowed(w)
			return
		}
		next(w, r)
	}
}
