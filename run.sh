#!/usr/bin/env bash
# Meniu pentru pornirea laboratoarelor din solutia PSSC.
set -u

cd "$(dirname "$0")"

# Docker Desktop pune comanda docker aici; nu e mereu in PATH.
export PATH="$PATH:/usr/local/bin:$HOME/.docker/bin:/Applications/Docker.app/Contents/Resources/bin"

LAB4_COMPOSE="Laborator4_PSSC/compose.yaml"
# Lab 5 uses the same SQL Server and database; its compose file adds the Stock column and demo products.
LAB5_COMPOSE="Laborator5_PSSC/compose.yaml"
# Lab 6 adds the cart status columns on top of Lab 5; Lab 7 uses the same database.
LAB6_COMPOSE="Laborator6_PSSC/compose.yaml"
# Lab 8 adds the invoicing tables and the Azure Service Bus emulator.
LAB8_COMPOSE="Laborator8_PSSC/compose.yaml"
# The earlier labs' compose files do not know the later labs' extra containers; no need to warn about them.
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

# Lab 7: the delivery API runs in the background, the cart API in the foreground; both stop together.
run_lab7() {
    local delivery_log="Laborator7_PSSC/delivery.log"
    echo
    echo "Pornesc API-ul de livrari (https://localhost:7297), log in $delivery_log ..."
    dotnet run --project Laborator7_PSSC/Laborator7_PSSC.Delivery >"$delivery_log" 2>&1 &
    for _ in $(seq 1 60); do
        curl -sk -o /dev/null https://localhost:7297/deliveries && break
        sleep 1
    done
    dotnet run --project Laborator7_PSSC/Laborator7_PSSC.Api
    pkill -f "Laborator7_PSSC.Delivery" 2>/dev/null
    echo "API-ul de livrari a fost oprit."
}

# Lab 8: the delivery API and both workers run in the background (logs in Laborator8_PSSC/*.log),
# the cart API in the foreground; everything stops together.
run_lab8() {
    echo "Astept emulatorul Service Bus..."
    for _ in $(seq 1 60); do
        curl -s http://localhost:5300/health 2>/dev/null | grep -q '"healthy"' && break
        sleep 3
    done
    if ! curl -s http://localhost:5300/health 2>/dev/null | grep -q '"healthy"'; then
        echo "Emulatorul nu a pornit. Detalii: docker compose -f $LAB8_COMPOSE logs servicebus-emulator"
        return 1
    fi

    echo
    echo "Pornesc in fundal: API livrari, worker facturare, worker livrare (log-uri in Laborator8_PSSC/*.log)..."
    dotnet run --project Laborator8_PSSC/Laborator8_PSSC.Delivery >Laborator8_PSSC/delivery.log 2>&1 &
    dotnet run --project Laborator8_PSSC/Laborator8_PSSC.Invoicing.EventProcessor >Laborator8_PSSC/invoicing.log 2>&1 &
    dotnet run --project Laborator8_PSSC/Laborator8_PSSC.Shipping.EventProcessor >Laborator8_PSSC/shipping.log 2>&1 &
    dotnet run --project Laborator8_PSSC/Laborator8_PSSC.Api
    pkill -f "Laborator8_PSSC.Delivery" 2>/dev/null
    pkill -f "Laborator8_PSSC.Invoicing.EventProcessor" 2>/dev/null
    pkill -f "Laborator8_PSSC.Shipping.EventProcessor" 2>/dev/null
    echo "API-ul de livrari si worker-ii au fost opriti."
}

while true; do
    echo
    echo "===== LABORATOARE PSSC ====="
    echo "1. Laborator 1"
    echo "2. Laborator 2"
    echo "3. Laborator 3"
    echo "4. Laborator 4 (porneste si baza de date)"
    echo "5. Laborator 5 (porneste si baza de date)"
    echo "6. Laborator 6 - API Web (porneste si baza de date, deschide Swagger)"
    echo "7. Laborator 7 - API cos + API livrari (porneste si baza de date)"
    echo "8. Laborator 8 - evenimente prin Service Bus (baza, emulator, API, worker-i)"
    echo "t. Ruleaza testele"
    echo "b. Compileaza tot"
    echo "s. Opreste baza de date si emulatorul (Lab 4-8)"
    echo "0. Iesire"
    printf "Optiune: "
    read -r option || break

    case "$option" in
        1) run_lab Laborator1_PSSC ;;
        2) run_lab Laborator2_PSSC ;;
        3) run_lab Laborator3_PSSC ;;
        4) start_database "$LAB4_COMPOSE" sqlserver-init && run_lab Laborator4_PSSC ;;
        5) start_database "$LAB5_COMPOSE" sqlserver-update && run_lab Laborator5_PSSC/Laborator5_PSSC.ConsoleApp ;;
        6) start_database "$LAB6_COMPOSE" sqlserver-update-lab6 && run_lab Laborator6_PSSC/Laborator6_PSSC.Api ;;
        7) start_database "$LAB6_COMPOSE" sqlserver-update-lab6 && run_lab7 ;;
        8) start_database "$LAB8_COMPOSE" sqlserver-update-lab8 && run_lab8 ;;
        t) dotnet test PSSC.sln ;;
        b) dotnet build PSSC.sln ;;
        s) docker compose -f "$LAB8_COMPOSE" down ;;
        0) break ;;
        *) echo "Optiune invalida." ;;
    esac
done
