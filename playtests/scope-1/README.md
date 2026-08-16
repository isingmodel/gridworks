# Scope 1 historical materials

Scope 1 is complete. Its current implementation rules are in
[`docs/scopes/SCOPE_1_INTERACTION.md`](../../docs/scopes/SCOPE_1_INTERACTION.md); the single official observation,
its limits and stable hashes are summarized in
[`docs/DEVELOPMENT_HISTORY.md`](../../docs/DEVELOPMENT_HISTORY.md).

This directory keeps the fixture checker and the Git-ignored `private/` app and engine logs. It does not
authorize another observation.

```sh
ruby verify_contract.rb
dotnet run --project ../../tools/Gridworks.Scope1Checks/Gridworks.Scope1Checks.csproj -c Release -- ../../data/scope-1-v1.json
```

Do not delete `private/` recursively.
