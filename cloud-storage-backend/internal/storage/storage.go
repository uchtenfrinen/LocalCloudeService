package storage

import (
	"fmt"
	"io"
	"os"
	"path/filepath"
	"strings"
	"time"
)

type FileInfo struct {
	Name     string    `json:"name"`
	Path     string    `json:"path"`
	IsDir    bool      `json:"is_dir"`
	Size     int64     `json:"size"`
	ModTime  time.Time `json:"mod_time"`
}

type Storage struct {
	root string
}

func New(root string) (*Storage, error) {
	abs, err := filepath.Abs(root)
	if err != nil {
		return nil, err
	}
	if err := os.MkdirAll(abs, 0o755); err != nil {
		return nil, fmt.Errorf("cannot create storage root: %w", err)
	}
	return &Storage{root: abs}, nil
}

func (s *Storage) resolve(relPath string) (string, error) {
	clean := filepath.Clean("/" + relPath)
	full := filepath.Join(s.root, clean)
	if !strings.HasPrefix(full, s.root) {
		return "", fmt.Errorf("path escapes storage root")
	}
	return full, nil
}

func (s *Storage) List(relPath string) ([]FileInfo, error) {
	full, err := s.resolve(relPath)
	if err != nil {
		return nil, err
	}
	entries, err := os.ReadDir(full)
	if err != nil {
		return nil, err
	}
	out := make([]FileInfo, 0, len(entries))
	for _, e := range entries {
		info, err := e.Info()
		if err != nil {
			continue
		}
		rel := filepath.Join(relPath, e.Name())
		out = append(out, FileInfo{
			Name:    e.Name(),
			Path:    rel,
			IsDir:   e.IsDir(),
			Size:    info.Size(),
			ModTime: info.ModTime(),
		})
	}
	return out, nil
}

func (s *Storage) Mkdir(relPath string) error {
	full, err := s.resolve(relPath)
	if err != nil {
		return err
	}
	return os.MkdirAll(full, 0o755)
}

func (s *Storage) Save(relPath string, reader io.Reader) error {
	full, err := s.resolve(relPath)
	if err != nil {
		return err
	}
	if err := os.MkdirAll(filepath.Dir(full), 0o755); err != nil {
		return err
	}
	dst, err := os.Create(full)
	if err != nil {
		return err
	}
	defer dst.Close()
	if _, err := io.Copy(dst, reader); err != nil {
		return fmt.Errorf("write %q: %w", relPath, err)
	}
	return nil
}

func (s *Storage) Open(relPath string) (*os.File, error) {
	full, err := s.resolve(relPath)
	if err != nil {
		return nil, err
	}
	return os.Open(full)
}

func (s *Storage) Delete(relPath string) error {
	full, err := s.resolve(relPath)
	if err != nil {
		return err
	}
	return os.RemoveAll(full)
}

func (s *Storage) Stat(relPath string) (*FileInfo, error) {
	full, err := s.resolve(relPath)
	if err != nil {
		return nil, err
	}
	info, err := os.Stat(full)
	if err != nil {
		return nil, err
	}
	return &FileInfo{
		Name:    info.Name(),
		Path:    relPath,
		IsDir:   info.IsDir(),
		Size:    info.Size(),
		ModTime: info.ModTime(),
	}, nil
}
