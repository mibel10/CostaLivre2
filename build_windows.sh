#!/usr/bin/env bash
set -euo pipefail
source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/build_common.sh"
run_unity_build "Win64" "CostaLivre.Editor.CostaPlatformBuild.Windows" "build_windows.log"
