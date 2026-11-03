#!/usr/bin/env bash
# Meniu pentru pornirea laboratoarelor din solutia PSSC.
set -u

cd "$(dirname "$0")"

# Docker Desktop pune comanda docker aici; nu e mereu in PATH.
export PATH="$PATH:/usr/local/bin:$HOME/.docker/bin:/Applications/Docker.app/Contents/Resources/bin"

LAB4_COMPOSE="Laborator4_PSSC/compose.yaml"
# Lab 5 uses the same SQL Server and database; its compose file adds the Stock column and demo products.
LAB5_COMPOSE="Laborator5_PSSC/compose.yaml"
# Lab 4's compose file does not know Lab 5's extra container; no need to warn about it.
export COMPOSE_IGNORE_ORPHANS=True

# $1 = compose file, $2 = the setup container to wait for.
start_database() {
    if ! command -v docker >/dev/null 2>&1; then
        echo "Docker nu este instalat. Instaleaza Docker Desktop."
        return 1
    fi

    if ! docker info >/dev/null 2>&1; then
        echo "Docker nu ruleaza, pornesc Docker Desktop..."
        open -a Docker 2>/dev/null
        for _ in $(seq 1 60); do
            docker info >/dev/null 2>&1 && break
            sleep 2
        done
        if ! docker info >/dev/null 2>&1; then
            echo "Docker nu a pornit. Deschide Docker Desktop manual si incearca din nou."
            return 1
        fi
    fi

    if [ ! -f Laborator4_PSSC/.env ]; then
        echo "Lipseste Laborator4_PSSC/.env (vezi Laborator4_PSSC/README.md)."
        return 1
    fi

    echo "Pornesc SQL Server..."
    # up -d asteapta ca serverul sa fie healthy inainte de scripturile de creare;
    # wait asteapta ultimul script si intoarce codul lui de iesire.
    if ! docker compose -f "$1" up -d \
        || ! docker compose -f "$1" wait "$2" >/dev/null; then
        echo "Baza de date nu a pornit. Detalii: docker compose -f $1 logs"
        return 1
    fi
}

run_lab() {
    echo
    dotnet run --project "$1"
}

while true; do
    echo
    echo "===== LABORATOARE PSSC ====="
    echo "1. Laborator 1"
    echo "2. Laborator 2"
    echo "3. Laborator 3"
    echo "4. Laborator 4 (porneste si baza de date)"
    echo "5. Laborator 5 (porneste si baza de date)"
    echo "t. Ruleaza testele"
    echo "b. Compileaza tot"
    echo "s. Opreste baza de date (Lab 4 si 5)"
    echo "0. Iesire"
    printf "Optiune: "
    read -r option || break

    case "$option" in
        1) run_lab Laborator1_PSSC ;;
        2) run_lab Laborator2_PSSC ;;
        3) run_lab Laborator3_PSSC ;;
        4) start_database "$LAB4_COMPOSE" sqlserver-init && run_lab Laborator4_PSSC ;;
        5) start_database "$LAB5_COMPOSE" sqlserver-update && run_lab Laborator5_PSSC ;;
        t) dotnet test PSSC.sln ;;
        b) dotnet build PSSC.sln ;;
        s) docker compose -f "$LAB5_COMPOSE" down ;;
        0) break ;;
        *) echo "Optiune invalida." ;;
    esac
done
