#!/usr/bin/env bash
# Project ACX instance control.
#   acx.sh init    <live|preview>   create acx/<name>.env with fresh passwords
#   acx.sh up      <live|preview>   build and start (first start downloads the world DB)
#   acx.sh down    <live|preview>   stop
#   acx.sh logs    <live|preview>   follow server output
#   acx.sh console <live|preview>   attach to the server console (detach: Ctrl+P Ctrl+Q)
#   acx.sh status
#   acx.sh backup  <live|preview>   dump auth+shard+world to $ACX_BACKUP_DIR
#   acx.sh apply-content <live|preview>  load acx/Content/**/*.sql into the world DB, then restart the server
#   acx.sh clone-live-to-preview    copy Live's characters and accounts into Preview
# Live runs on the Live laptop (ssh host acx-live, over Tailscale) since 2026-11-17. On a machine without acx/live.env,
# the live commands run there: up first sends it main (git archive), the others run its own acx.sh, and backup and
# clone-live-to-preview dump its database over SSH.
set -euo pipefail

ACX_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ACX_BACKUP_DIR="${ACX_BACKUP_DIR:-/run/media/jj/HDDStorage/acx-backups}"
ACX_LIVE_SSH="${ACX_LIVE_SSH:-acx-live}"
ACX_LIVE_TREE=project-acx   # on the Live laptop, in its home

die() { echo "acx: $*" >&2; exit 1; }

live_remote() { [[ "$1" == live && ! -f "$ACX_DIR/live.env" ]]; }

# the Live laptop's own acx.sh
remote_acx() { ssh "$ACX_LIVE_SSH" "$(printf '%q ' "$ACX_LIVE_TREE/acx/scripts/acx.sh" "$@")"; }

# Live runs main: replace the laptop's tree with main's, keeping its live.env
deploy_live() {
    local root; root="$(git -C "$ACX_DIR" rev-parse --show-toplevel)"
    echo "deploy: main $(git -C "$root" rev-parse --short main) to $ACX_LIVE_SSH"
    git -C "$root" archive --format=tar main | ssh "$ACX_LIVE_SSH" "set -e
        rm -rf $ACX_LIVE_TREE.new $ACX_LIVE_TREE.old
        mkdir $ACX_LIVE_TREE.new
        tar -x -C $ACX_LIVE_TREE.new
        cp -p $ACX_LIVE_TREE/acx/live.env $ACX_LIVE_TREE.new/acx/live.env
        echo $(git -C "$root" rev-parse main) > $ACX_LIVE_TREE.new/.acx-deployed-main
        mv $ACX_LIVE_TREE $ACX_LIVE_TREE.old
        mv $ACX_LIVE_TREE.new $ACX_LIVE_TREE"
}

# Live's database dumped over SSH ($1: the databases)
remote_live_dump() {
    ssh "$ACX_LIVE_SSH" "docker exec acx-live-db-1 sh -c 'exec mysqldump -uroot -p\"\$MYSQL_ROOT_PASSWORD\" --single-transaction --routines --databases $1'"
}

instance_env() {
    local name="$1"
    [[ "$name" == live || "$name" == preview ]] || die "instance must be live or preview"
    local env="$ACX_DIR/$name.env"
    [[ -f "$env" ]] || die "$env missing; run: acx.sh init $name"
    echo "$env"
}

compose() {
    local name="$1"; shift
    local env; env="$(instance_env "$name")"
    docker compose -p "acx-$name" --env-file "$env" -f "$ACX_DIR/compose.yml" "$@"
}

env_value() { grep -E "^$2=" "$1" | head -1 | cut -d= -f2-; }

cmd_init() {
    local name="$1" env="$ACX_DIR/$1.env"
    [[ "$name" == live || "$name" == preview ]] || die "instance must be live or preview"
    [[ -f "$env" ]] && die "$env already exists"
    local port=9000 dbport=3306
    [[ "$name" == preview ]] && port=9010 dbport=3307
    sed -e "s|^ACX_INSTANCE=.*|ACX_INSTANCE=$name|" \
        -e "s|^ACX_ENV_FILE=.*|ACX_ENV_FILE=$name.env|" \
        -e "s|^ACX_DATA_DIR=.*|ACX_DATA_DIR=$HOME/acx-data/$name|" \
        -e "s|^ACX_GAME_PORT=.*|ACX_GAME_PORT=$port|" \
        -e "s|^ACX_GAME_PORT_2=.*|ACX_GAME_PORT_2=$((port + 1))|" \
        -e "s|^ACX_DB_PORT=.*|ACX_DB_PORT=$dbport|" \
        -e "s|^MYSQL_ROOT_PASSWORD=.*|MYSQL_ROOT_PASSWORD=$(openssl rand -hex 16)|" \
        -e "s|^MYSQL_PASSWORD=.*|MYSQL_PASSWORD=$(openssl rand -hex 16)|" \
        "$ACX_DIR/instance.env.example" > "$env"
    [[ "$name" == preview ]] && sed -i "s|^ACE_WORLD_NAME=.*|ACE_WORLD_NAME=Project ACX Preview|" "$env"
    chmod 600 "$env"
    mkdir -p "$HOME/acx-data/$name"/{db,Config,Logs,Mods,Pathfinding}
    echo "created $env (data in ~/acx-data/$name, game port $port)"
}

dump() {
    local name="$1" out="$2"
    if live_remote "$name"; then
        remote_live_dump "ace_auth ace_shard ace_world" > "$out"
        return
    fi
    instance_env "$name" > /dev/null
    compose "$name" exec -T db sh -c 'exec mysqldump -uroot -p"$MYSQL_ROOT_PASSWORD" --single-transaction --routines --databases ace_auth ace_shard ace_world' > "$out"
}

cmd_backup() {
    local name="$1"
    mkdir -p "$ACX_BACKUP_DIR" 2>/dev/null || die "can't create $ACX_BACKUP_DIR (is the HDD mounted? udisksctl mount -b /dev/disk/by-label/HDDStorage)"
    local out; out="$ACX_BACKUP_DIR/acx-$name-$(date +%Y%m%d-%H%M%S).sql"
    dump "$name" "$out"
    gzip "$out"
    echo "backup: $out.gz"
}

cmd_apply_content() {
    local name="$1" f
    while IFS= read -r f; do
        echo "content: ${f#"$ACX_DIR/"}"
        compose "$name" exec -T db sh -c 'exec mysql -uroot -p"$MYSQL_ROOT_PASSWORD" ace_world' < "$f"
    done < <(find "$ACX_DIR/Content" -name '*.sql' | sort)
    compose "$name" restart server
}

cmd_clone_live_to_preview() {
    local tmp; tmp="$(mktemp -d)"
    trap 'rm -rf "$tmp"' RETURN
    if live_remote live; then
        remote_live_dump "ace_auth ace_shard" > "$tmp/live.sql"
    else
        compose live exec -T db sh -c 'exec mysqldump -uroot -p"$MYSQL_ROOT_PASSWORD" --single-transaction --databases ace_auth ace_shard' > "$tmp/live.sql"
    fi
    compose preview stop server
    compose preview exec -T db sh -c 'exec mysql -uroot -p"$MYSQL_ROOT_PASSWORD"' < "$tmp/live.sql"
    compose preview start server
    echo "preview now has Live's accounts and characters (world DB left as Preview's own)"
}

if [[ "${2:-}" == live ]] && live_remote live; then
    case "$1" in
        init)          die "Live is on $ACX_LIVE_SSH" ;;
        up)            deploy_live; remote_acx up live; exit ;;
        down|apply-content) remote_acx "$1" live; exit ;;
        logs|console)  exec ssh -t "$ACX_LIVE_SSH" "$ACX_LIVE_TREE/acx/scripts/acx.sh $1 live" ;;
    esac
fi

case "${1:-}" in
    init)    cmd_init "${2:?instance}" ;;
    up)      compose "${2:?instance}" up -d --build ;;
    down)    compose "${2:?instance}" down ;;
    logs)    compose "${2:?instance}" logs -f server ;;
    console) docker attach "$(compose "${2:?instance}" ps -q server)" ;;
    status)  docker ps --filter name=acx- --format 'table {{.Names}}\t{{.Status}}\t{{.Ports}}'
             if live_remote live; then
                 echo "on $ACX_LIVE_SSH:"
                 ssh "$ACX_LIVE_SSH" "docker ps --filter name=acx- --format 'table {{.Names}}\t{{.Status}}\t{{.Ports}}'"
             fi ;;
    backup)  cmd_backup "${2:?instance}" ;;
    apply-content) cmd_apply_content "${2:?instance}" ;;
    clone-live-to-preview) cmd_clone_live_to_preview ;;
    *) sed -n '2,11p' "${BASH_SOURCE[0]}"; exit 1 ;;
esac
