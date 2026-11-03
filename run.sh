#!/usr/bin/env bash
# Meniu pentru pornirea laboratoarelor din solutia PSSC.
set -u

cd "$(dirname "$0")"

# Docker Desktop pune comanda docker aici; nu e mereu in PATH.
export PATH="$PATH:/usr/local/bin:$HOME/.docker/bin:/Applications/Docker.app/Contents/Resources/bin"

LAB4_COMPOSE="Laborator4_PSSC/compose.yaml"

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
    # up -d asteapta ca serverul sa fie healthy inainte de sqlserver-init;
    # wait asteapta scriptul de creare si intoarce codul lui de iesire.
    if ! docker compose -f "$LAB4_COMPOSE" up -d \
        || ! docker compose -f "$LAB4_COMPOSE" wait sqlserver-init >/dev/null; then
        echo "Baza de date nu a pornit. Detalii: docker compose -f $LAB4_COMPOSE logs"
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
    echo "t. Ruleaza testele"
    echo "b. Compileaza tot"
    echo "s. Opreste baza de date (Lab 4)"
    echo "0. Iesire"
    printf "Optiune: "
    read -r option || break

    case "$option" in
        1) run_lab Laborator1_PSSC ;;
        2) run_lab Laborator2_PSSC ;;
        3) run_lab Laborator3_PSSC ;;
        4) start_database && run_lab Laborator4_PSSC ;;
        t) dotnet test PSSC.sln ;;
        b) dotnet build PSSC.sln ;;
        s) docker compose -f "$LAB4_COMPOSE" down ;;
        0) break ;;
        *) echo "Optiune invalida." ;;
    esac
done
