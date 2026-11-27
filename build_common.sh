#!/usr/bin/env bash
set -euo pipefail

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BUILDS_DIR="$PROJECT_ROOT/Builds"
mkdir -p "$BUILDS_DIR"

resolve_unity() {
    if [[ -n "${UNITY_EXE:-}" && -x "$UNITY_EXE" ]]; then
        echo "$UNITY_EXE"
        return 0
    fi

    local version="6000.3.29f1"
    local version_file="$PROJECT_ROOT/ProjectSettings/ProjectVersion.txt"
    if [[ -f "$version_file" ]]; then
        local parsed
        parsed="$(grep -m1 '^m_EditorVersion:' "$version_file" | awk '{print $2}' | tr -d '\r')"
        if [[ -n "$parsed" ]]; then
            version="$parsed"
        fi
    fi

    local candidates=(
        "$HOME/Unity/Hub/Editor/$version/Editor/Unity"
        "/opt/Unity/Hub/Editor/$version/Editor/Unity"
        "/opt/unity/Editor/Unity"
        "/usr/bin/unity-editor"
    )

    for c in "${candidates[@]}"; do
        if [[ -x "$c" ]]; then
            echo "$c"
            return 0
        fi
    done

    echo "ERRO: Unity $version nao encontrado no CachyOS/Linux." >&2
    echo "Instale o Unity $version pelo Unity Hub (ou defina UNITY_EXE=/caminho/para/Unity)." >&2
    exit 1
}

run_unity_build() {
    local target="$1"
    local method="$2"
    local log_name="$3"
    local unity
    unity="$(resolve_unity)"
    local log_file="$BUILDS_DIR/$log_name"

    echo "==> Iniciando Unity ($target -> $method)..."
    echo "    Unity: $unity"
    echo "    Log:   $log_file"

    if ! "$unity" \
        -batchmode \
        -quit \
        -projectPath "$PROJECT_ROOT" \
        -buildTarget "$target" \
        -executeMethod "$method" \
        -logFile "$log_file"; then
        echo "ERRO: Build $target falhou. Ultimas linhas do log:" >&2
        tail -n 40 "$log_file" >&2 || true
        exit 1
    fi

    echo "OK: Build $target concluido!"
}
