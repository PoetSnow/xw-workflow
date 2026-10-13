#!/usr/bin/env bash
# Architecture gate: 跨模块禁止引用对方 Domain（仅允许同模块 Domain / XW.Shared.Domain）
# 规则来源: docs/XW-项目说明-V1.0.md §3
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"

violations=0

# 从 csproj 路径得到“模块键”
# Modules/XW.Sales/... → XW.Sales
# Services/XW.Integration/... → XW.Integration
# Shared/... → XW.Shared
# XW.Host.API → XW.Host
module_key_from_path() {
  local path="$1"
  if [[ "$path" =~ src/Modules/(XW\.[^/]+)/ ]]; then
    echo "${BASH_REMATCH[1]}"
    return
  fi
  if [[ "$path" =~ src/Services/(XW\.[^/.]+) ]]; then
    echo "${BASH_REMATCH[1]}"
    return
  fi
  if [[ "$path" =~ src/Shared/ ]]; then
    echo "XW.Shared"
    return
  fi
  if [[ "$path" =~ src/XW\.Host ]]; then
    echo "XW.Host"
    return
  fi
  echo "UNKNOWN"
}

# Domain 项目名 → 模块键（去掉末尾 .Domain）
# XW.Sales.Domain → XW.Sales
# XW.Shared.Domain → XW.Shared
domain_module_key() {
  local name="$1"
  echo "${name%.Domain}"
}

is_domain_project() {
  local name="$1"
  [[ "$name" == *.Domain ]]
}

check_csproj() {
  local csproj="$1"
  local src_module
  src_module="$(module_key_from_path "$csproj")"

  # 抽取 ProjectReference Include="...Something.csproj"
  # csproj 里常见 Windows 反斜杠；Linux 上 basename 不会按 \ 分段，必须先规范化
  while IFS= read -r include; do
    [[ -z "$include" ]] && continue
    local include_norm ref_name
    include_norm="${include//\\//}"
    ref_name="$(basename "$include_norm" .csproj)"

    if ! is_domain_project "$ref_name"; then
      continue
    fi

    if [[ "$ref_name" == "XW.Shared.Domain" ]]; then
      continue
    fi

    local tgt_module
    tgt_module="$(domain_module_key "$ref_name")"

    if [[ "$src_module" == "$tgt_module" ]]; then
      continue
    fi

    # 跨模块 / Service·Host 引用业务 Domain
    echo "VIOLATION: $csproj"
    echo "  module [$src_module] → ProjectReference Domain [$ref_name] (module [$tgt_module])"
    echo "  rule: cross-module Domain refs forbidden; use *.Application.Contracts only"
    violations=$((violations + 1))
  done < <(grep -oE 'ProjectReference Include="[^"]+\.csproj"' "$csproj" \
    | sed -E 's/ProjectReference Include="([^"]+)"/\1/' \
    || true)
}

echo "== Architecture gate: no cross-module Domain ProjectReference =="

mapfile -t projects < <(
  find src/Modules src/Services src/XW.Host.API -name '*.csproj' 2>/dev/null | sort
)

if [[ ${#projects[@]} -eq 0 ]]; then
  echo "ERROR: no csproj found under src/Modules, src/Services, src/XW.Host.API"
  exit 1
fi

for p in "${projects[@]}"; do
  check_csproj "$p"
done

if [[ "$violations" -gt 0 ]]; then
  echo
  echo "FAILED: $violations cross-module Domain reference(s)"
  exit 1
fi

echo "OK: no cross-module Domain references (${#projects[@]} projects scanned)"
exit 0
