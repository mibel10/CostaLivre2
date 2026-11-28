#!/usr/bin/env bash
set -euo pipefail
source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/build_common.sh"
run_unity_build "Linux64" "CostaLivre.Editor.CostaPlatformBuild.Linux" "build_linux.log"
