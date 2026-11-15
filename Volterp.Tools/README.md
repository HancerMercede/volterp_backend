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

## The connection string

The command reads `ConnectionStrings:DefaultConnection`, the same key the API
reads, from `appsettings.json` next to the executable or from the environment
(`ConnectionStrings__DefaultConnection`).

The migrations have to be applied before the command runs: the API applies them
when it starts.

## The exit code

`0` when the tenant was created or was already there, `1` when it was refused,
`2` when the arguments or the connection string are missing.
