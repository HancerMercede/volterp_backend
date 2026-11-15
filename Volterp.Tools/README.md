# Volterp.Tools

The deployment commands of the ERP. They run on their own: they do not need
the API to be up, and they talk to the same database the API talks to.

## The first tenant of a client

`create-tenant` creates the company with the RNC it really has and its first
administrator:

```
dotnet run --project Volterp.Tools -- create-tenant \
  --rnc 101672919 \
  --name "Distribuidora del Norte" \
  --legal-name "Distribuidora del Norte S.R.L." \
  --address "Av. Duarte No. 45, Santiago" \
  --phone "809-555-0202" \
  --email "contacto@distnorte.do" \
  --admin-username admin \
  --admin-email admin@distnorte.do \
  --admin-name "Administrador"
```

The RNC is validated with the value object of the domain (nine digits for a
legal person, eleven for a natural person, with the check digit): an invalid
one is refused before anything is written.

The password of the administrator is generated and shown **once**, on the
screen of whoever runs the command. It is not written to any log and it is not
stored anywhere but in the hash of the database.

The command is idempotent: run it again with the same RNC and it says that the
company is already there, without creating anything.

## The demo invoices of the fiscal banner

`seed-demo-invoices` leaves one electronic invoice in each open fiscal state
for the tenant with the given RNC:

```
dotnet run --project Volterp.Tools -- seed-demo-invoices --rnc 101500018
```

The states are pending emission, in process, rejected and requires review. The
endpoint `GET /api/ElectronicInvoices/state` counts and lists those four
invoices for the tenant of the token.

What the command writes is four consumption e-CFs (`E320000009001` to
`E320000009004`) and the sale each one points to. The sales are completed
sales of 100 plus 18 of ITBIS, without lines: the demo needs a sale to point
at, not a sale to invoice. They carry the code `DEMO-EC-PENDING`,
`DEMO-EC-IN-PROCESS`, `DEMO-EC-REJECTED` and `DEMO-EC-REVIEW`.

The tenant is the one with the given RNC. Once the company is resolved by
that RNC, every read and every write carries its company id, so the tenant of
whoever runs the command is irrelevant and the command cannot read or touch
the invoices of another company.

The command is idempotent: a second run finds the same four invoices and
creates nothing, not even a second sale. It announces the database before it
writes, and it prints how many invoices it created, how many were already
there and how many of the four are in each state. Those numbers are the ones
of the demo set, not the totals of the tenant.

The demo invoices are not signed. The seeder never loads a certificate, never
signs and never talks to the DGII. Their printed representation is therefore
unavailable, while the fiscal state shown by the banner is available. The
pending and in-process fixtures are kept out of the queues that talk to the
DGII. The e-NCFs are demo numbers: they belong to no range the DGII
authorized, and they never travel to the DGII.

The command does not migrate: the schema has to be there before it runs.

## The connection string

The command reads `ConnectionStrings:DefaultConnection`, the same key the API
reads, from `appsettings.json` next to the executable or from the environment
(`ConnectionStrings__DefaultConnection`).

The migrations have to be applied before the command runs: the API applies them
when it starts.

## The exit code

`0` when the tenant was created or was already there, and when the demo
invoices were written or were already there. `1` when the command was refused:
an invalid RNC, a company that does not exist, or a write that failed. `2`
when the arguments or the connection string are missing.
