# GuardRelay

<p align="center">
  <a href="https://github.com/AnderssonPeter/GuardRelay">
    <h3>GuardRelay</h3>
  </a>


  <p align="center">
    Service that reads Charge Amps Amp Guard and sends it to MQTT (With Home Assistant device creation)
    <br />
    <br />
    ·
    <a href="https://github.com/AnderssonPeter/GuardRelay/issues">Report Bug</a>
    ·
    <a href="https://github.com/AnderssonPeter/GuardRelay/issues">Request Feature</a>
    ·
  </p>
</p>
<br />

[![GitHub Tag](https://img.shields.io/github/v/tag/anderssonpeter/guardrelay)](https://github.com/AnderssonPeter/GuardRelay/pkgs/container/guardrelay)
[![GitHub Actions Workflow Status](https://img.shields.io/github/actions/workflow/status/anderssonpeter/GuardRelay/.github%2Fworkflows%2Fpublish.yml)](https://github.com/AnderssonPeter/GuardRelay/actions/workflows/publish.yml)
[![GitHub License](https://img.shields.io/github/license/anderssonpeter/GuardRelay)](https://github.com/AnderssonPeter/GuardRelay/blob/main/LICENSE)


## Table of Contents
* [About the Project](#about-the-project)
* [Getting Started](#getting-started)

## About The Project
Service that reads Charge Amps Amp Guard and sends it to MQTT (With Home Assistant device creation).
It currently creates the following entities in home assistant:
* Current L1 (A)
* Current L2 (A)
* Current L3 (A)
* Current Total (A)
* Voltage L1 (V)
* Voltage L2 (V)
* Voltage L3 (V)
* Voltage Total (V)
* Power L1 (kWh)
* Power L2 (kWh)
* Power L3 (kWh)
* Power Total (kWh)
* Phase Angle L1 (degrees)
* Phase Angle L2 (degrees)
* Phase Angle L3 (degrees)

## Getting Started

```yaml
version: "3.8"
services:
  guardrelay:
    container_name: guardrelay
    image: "ghcr.io/anderssonpeter/guardrelay:latest"
    restart: unless-stopped
    mem_limit: 128M
    cpus: 0.5
    security_opt:
      - no-new-privileges:true
    environment:
      - TZ=Europe/Stockholm
      - Application__Application=/config/GuardRelay.sqlite
      - MQTT__Connection__Server=hostname
      - ChargeAmpGuard__Uri=ws://192.168.0.15/ws
      - ChargeAmpGuard__Pin=123456
    volumes:
      - ./guardrelay/config:/config
```

## Todo:
[ ] Healthcheck
[ ] Dotnet format
[ ] Loki logging
[ ] Document all settings
