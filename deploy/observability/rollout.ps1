# Usage: .\rollout.ps1 <host-ip> <env: prod|test|infra> <vl-endpoint> [-Tag 20261128]
# Backs up /opt/vector, validates the new config in a throwaway container, switches over,
# then waits for records of this host to show up in VictoriaLogs.
param(
  [Parameter(Mandatory)][string]$HostIp,
  [Parameter(Mandatory)][string]$LogEnv,
  [Parameter(Mandatory)][string]$VlEndpoint,
  [string]$Tag = (Get-Date -Format yyyyMMdd)
)
$ErrorActionPreference = 'Stop'
$dir = Join-Path $PSScriptRoot 'vector'
$ssh = "root@$HostIp"
$o = '-o', 'BatchMode=yes'
$img = 'timberio/vector:0.52.0-alpine'

ssh @o $ssh "mkdir -p /opt/vector/data; [ -e /opt/vector.bak-$Tag ] || cp -a /opt/vector /opt/vector.bak-$Tag"
scp @o "$dir/vector.yaml" "$ssh`:/opt/vector/vector.yaml.new"
scp @o "$dir/docker-compose.yml" "$ssh`:/opt/vector/docker-compose.yml.new"

$remote = @"
true
set -e
cd /opt/vector
docker run --rm -e LOG_HOST=x -e LOG_ENV=x -e VL_ENDPOINT=$VlEndpoint --tmpfs /var/lib/vector -v /var/run/docker.sock:/var/run/docker.sock:ro -v /opt/vector/vector.yaml.new:/etc/vector/vector.yaml:ro $img validate /etc/vector/vector.yaml
mv vector.yaml.new vector.yaml; mv docker-compose.yml.new docker-compose.yml
printf 'LOG_HOST=%s\nLOG_ENV=%s\nVL_ENDPOINT=%s\n' "`$(hostname)" '$LogEnv' '$VlEndpoint' > .env
docker compose up -d
sleep 45
H=`$(hostname)
curl -s "$VlEndpoint/select/logsql/query" --data-urlencode "query=host:=`$H env:=$LogEnv" -d start=-1m -d limit=3 | cut -c1-300
"@
$ErrorActionPreference = 'Continue'
$remote | ssh @o $ssh "tr -d '\r' | bash"



