# Volterp.DgiiSimulator

An emulator of the **taxpayer services** of the DGII (the family a taxpayer
consumes), so the e-CF circuit can be exercised end to end without DGII
credentials.

It is written from the official contract — `docs/DescTec-Servicios-DGII.txt` —
and **not** from `DgiiClient`: a simulator copied from the client only proves
that the client agrees with itself.

It is a standalone project. It is not in `Volterp.slnx`, it references nothing
of the ERP, and it never runs inside the production API.

## The endpoints

Every path is the one the official document writes, in its lower case. The
environment segment (`testecf`, `certecf`, `ecf`) comes first.

| Method | Path | Document |
|---|---|---|
| GET | `/{ambiente}/autenticacion/api/autenticacion/semilla` | p. 10 |
| POST | `/{ambiente}/autenticacion/api/autenticacion/validarsemilla` | p. 11 |
| POST | `/{ambiente}/recepcion/api/facturaselectronicas` | p. 13 |
| GET | `/{ambiente}/consultaresultado/api/consultas/estado?trackid=` | p. 22 |
| GET | `/{ambiente}/consultatrackids/api/trackids/consulta?rncemisor=&encf=` | p. 30 |
| GET | `/{ambiente}/consultaestado/api/consultas/estado?rncemisor=&ncfelectronico=` | p. 27 |

The **emisor-receptor** family (`/fe/autenticacion/api/semilla`,
`/fe/recepcion/api/ecf`, …) is a different contract: that one Volterp
publishes for the DGII to call, and it is not simulated here.

The router of ASP.NET matches the literals without regard for case, so a path
that differs only in case is refused here on purpose: a client with the case
wrong has to find out against the simulator and not in production. That
covers the service path **and** the environment segment, which is one of the
three the DGII publishes (`testecf`, `certecf`, `ecf`), written the way the
document writes them.

## What the simulator answers

- **Seed**: `<SemillaModel>` with `<valor>` and `<fecha>`, as the XML the
  document shows.
- **Token**: `{token, expedido, expira}`, valid for **one hour** (p. 9). The
  token carries the simulator's own signature; the seed is validated by the
  value it holds **inside the signed file**, which is where the document asks
  it to be. A seed that was not issued, an unsigned file or a file whose
  signature does not verify is refused.
- **Reception**: `{trackId, error, mensaje}` with a track id that is a GUID
  (p. 13). The file has to arrive as the multipart field `xml` and its name
  has to be `RNC+e-NCF.xml` (p. 13). A document that does not say its issuer
  RNC and its e-NCF is refused.
- **Status by track id**: `{trackId, codigo, estado, rnc, eNCF,
  secuenciaUtilizada, fechaRecepcion, mensajes}` (p. 22).
- **Track ids by e-NCF**: `{trackId, estado, fechaRecepcion}` (p. 30). The
  track id of a document that is not there comes back empty.
- **State of a document**: `{codigo, estado, rncEmisor, ncfElectronico,
  montoTotal, totalITBIS, fechaEmision, fechaFirma, rncComprador,
  codigoSeguridad, idExtranjero}` (p. 27). The security code is the **first
  six characters of the SignatureValue**, taken as they are, without leaving
  out any character: it is the same code the taxpayer prints next to the QR
  (`Volterp.ElectronicInvoicing/Signing/EcfSecurityCode.cs`).

### The verdicts

The five states the DGII publishes (p. 25 and p. 29):

| Code | State |
|---|---|
| 0 | No encontrado |
| 1 | Aceptado |
| 2 | Rechazado |
| 3 | En Proceso |
| 4 | Aceptado Condicional |

A rejected document carries the message and the code of the rejection in
`mensajes`.

`secuenciaUtilizada` says whether the number of the sequence can be used
again: **true means it cannot** (p. 25). A document that is valid took the
number, so it reports true; a document that was rejected left the number free,
which is the scenario the document describes.

### The delegation

The consultations by e-NCF (`consultatrackids` and `consultaestado`) only
answer the session that sent the document. The DGII asks the caller to be
delegated for the emitter or the buyer (p. 26 and p. 30); the session that
traded a signed seed for a token is what the simulator has instead of a
delegation. The consultation by track id is not scoped that way: the document
does not ask it to be (p. 22), and the track id is the handle the DGII issued.

## The knobs

They are configuration and request headers. **The simulator does not add
endpoints of its own.**

| Knob | Where | What it does |
|---|---|---|
| `BaseUrl` | configuration | Where it listens. Loopback (`http://127.0.0.1:5090`) unless it is configured otherwise. |
| `Environment` | configuration | The environment it advertises at startup. The segment of the request is taken as it comes, because the DGII's environments differ only by host. |
| `DefaultVerdict` | configuration | The verdict a document gets when the request does not ask for another one. |
| `RejectionMessage`, `RejectionCode` | configuration | The message and the code a rejection carries. |
| `InProcessDurationMilliseconds` | configuration | How long a document stays "En Proceso" before the verdict becomes final. Zero keeps it in process. |
| `VerdictAfterProcessing` | configuration | The verdict a document ends with when that window closes. |
| `SeedDelayMilliseconds`, `ReceptionDelayMilliseconds`, `QueryDelayMilliseconds` | configuration | The delay each operation holds the caller with. |
| `FailureRate` | configuration | The share of requests answered with a server error, between zero and one. |
| `AllowedIssuerRnc` | configuration | The only issuer RNC documents are accepted from. Empty accepts every issuer. |
| `SeedLifetimeSeconds` | configuration | How long a seed stays valid. |
| `TokenLifetimeMinutes` | configuration | How long the token lives. The DGII grants one hour. |
| `X-DGII-Simulator-Verdict` | header of the reception | The verdict of the received document: `accepted`, `rejected`, `in-process`, `accepted-conditionally`. |
| `X-DGII-Simulator-Error` | header of any call | The status the simulator answers with instead of doing its work. |

The two headers are **not** part of the DGII's contract: they are how a test
turns a condition the simulator cannot be asked for over its own endpoints.

## Running it

```bash
dotnet run --project Volterp.DgiiSimulator -c Release
```

The settings come from `appsettings.json` and from the environment, with the
usual ASP.NET names:

```bash
DgiiSimulator__BaseUrl=http://0.0.0.0:5090 \
DgiiSimulator__FailureRate=0.2 \
dotnet run --project Volterp.DgiiSimulator -c Release
```

## The tests

```bash
dotnet test Volterp.DgiiSimulator/tests/Volterp.DgiiSimulator.Tests.csproj -c Release
```

## The credentials

There are none. The certificate the simulator signs its tokens with is
generated in memory when it starts, and no key file of anybody is in this
project.
