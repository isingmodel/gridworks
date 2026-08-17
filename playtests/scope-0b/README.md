# Scope 0B historical materials

Scope 0B is complete. Its current implementation rules are in
[`docs/scopes/SCOPE_0B_PLAYABLE.md`](../../docs/scopes/SCOPE_0B_PLAYABLE.md); the execution decision, evidence
limits and stable hashes are in [`docs/DEVELOPMENT_HISTORY.md`](../../docs/DEVELOPMENT_HISTORY.md).

This directory keeps the frozen participant `FACILITATOR_SHEET.md`, historical `record-template.csv`, the
fixture checker and the Git-ignored `private/` evidence directory. They do not authorize another run.
The frozen sheet mentions the deleted historical `verify_implementation.rb`; restore it only from the
pre-compression Git snapshot named in the development history, and do not treat it as a current check.

Current checks:

```sh
ruby verify_contract.rb
dotnet run --project ../../tools/Gridworks.Checks/Gridworks.Checks.csproj -c Release
```

`private/` may contain the only local copies of app and engine logs. Do not delete it recursively.
