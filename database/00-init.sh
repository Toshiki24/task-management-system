#!/bin/bash
# ============================================================
# PostgreSQLコンテナ初回起動時に実行される初期化スクリプト。
#
# docker-entrypoint-initdb.d はトップレベルの .sh / .sql のみを処理し
# サブディレクトリは無視されるため、
# migrations/ と seed/ を明示的にこのスクリプトから実行する。
# ============================================================
set -euo pipefail

run_sql_dir() {
    local dir="$1"
    if [ -d "$dir" ]; then
        for f in "$dir"/*.sql; do
            [ -e "$f" ] || continue
            echo "Running $f"
            psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" -f "$f"
        done
    fi
}

run_sql_dir "/docker-entrypoint-initdb.d/migrations"
run_sql_dir "/docker-entrypoint-initdb.d/seed"
