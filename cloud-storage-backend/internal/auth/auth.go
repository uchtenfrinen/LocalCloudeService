package auth

import (
	"net/http"
	"strings"
)

type Auth struct {
	token string
}

func New(token string) *Auth {
	return &Auth{token: token}
}

func (a *Auth) Middleware(next http.Handler) http.Handler {
	return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		authHeader := r.Header.Get("Authorization")
		if authHeader == "" {
			writeUnauthorized(w, "missing Authorization header")
			return
		}

		parts := strings.SplitN(authHeader, " ", 2)
		if len(parts) != 2 || strings.ToLower(parts[0]) != "bearer" {
			writeUnauthorized(w, "invalid Authorization format, use 'Bearer <token>'")
			return
		}

		if !compareToken(parts[1], a.token) {
			writeUnauthorized(w, "invalid token")
			return
		}

		next.ServeHTTP(w, r)
	})
}

func compareToken(a, b string) bool {
	if len(a) != len(b) {
		return false
	}
	var diff byte
	for i := 0; i < len(a); i++ {
		diff |= a[i] ^ b[i]
	}
	return diff == 0
}

func writeUnauthorized(w http.ResponseWriter, msg string) {
	w.Header().Set("Content-Type", "application/json")
	w.WriteHeader(http.StatusUnauthorized)
	w.Write([]byte(`{"error":"` + msg + `"}`))
}
