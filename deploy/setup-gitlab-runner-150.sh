#!/usr/bin/env bash
# 在 192.168.1.150 上执行：安装并注册 GitLab Runner（shell）
# 用法：
#   export GITLAB_URL=http://你的内网GitLab
#   export REGISTRATION_TOKEN=项目或群组里的 Runner token
#   sudo -E bash deploy/setup-gitlab-runner-150.sh
set -euo pipefail

GITLAB_URL="${GITLAB_URL:?set GITLAB_URL e.g. http://192.168.1.x}"
REGISTRATION_TOKEN="${REGISTRATION_TOKEN:?set REGISTRATION_TOKEN from GitLab → Settings → CI/CD → Runners}"

if ! command -v gitlab-runner >/dev/null 2>&1; then
  curl -L https://packages.gitlab.com/install/repositories/runner/gitlab-runner/script.deb.sh | bash
  apt-get install -y gitlab-runner
fi

# 常用构建依赖（按需）
apt-get update
apt-get install -y curl ca-certificates apt-transport-https

# .NET 8（若尚未安装）
if ! command -v dotnet >/dev/null 2>&1; then
  wget https://dot.net/v1/dotnet-install.sh -O /tmp/dotnet-install.sh
  bash /tmp/dotnet-install.sh --channel 8.0
  ln -sf "$HOME/.dotnet/dotnet" /usr/local/bin/dotnet || true
  # root 跑 runner 时：
  export DOTNET_ROOT=/root/.dotnet
  echo 'export DOTNET_ROOT=/root/.dotnet' >> /etc/profile.d/dotnet.sh
  echo 'export PATH=$PATH:/root/.dotnet:/root/.dotnet/tools' >> /etc/profile.d/dotnet.sh
fi

# Node 20 + pnpm
if ! command -v node >/dev/null 2>&1; then
  curl -fsSL https://deb.nodesource.com/setup_20.x | bash -
  apt-get install -y nodejs
fi
npm install -g pnpm@9 || true

gitlab-runner register --non-interactive \
  --url "$GITLAB_URL" \
  --token "$REGISTRATION_TOKEN" \
  --executor shell \
  --description "xw-150-shell" \
  --tag-list "xw-150" \
  --run-untagged=false

# gitlab-runner 用户要能跑 docker
usermod -aG docker gitlab-runner || true

mkdir -p /opt/xw
if [ ! -f /opt/xw/.env ]; then
  echo "Create /opt/xw/.env from repo .env.example (MySQL/Redis/Rabbit → 192.168.1.150)"
fi

gitlab-runner start || systemctl enable --now gitlab-runner
gitlab-runner status || true
echo "Done. Push code to GitLab and open CI/CD → Pipelines."
