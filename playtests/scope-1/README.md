# Scope 1 historical materials

Scope 1 is complete. The single official observation and its evidence boundary are summarized in the
[compressed history](../../docs/archive/COMPLETED_HISTORY.md). The detailed former scope and history are
recoverable from Git commit `9aceaf7` and are not current implementation authority.

This directory keeps the fixture checker and the Git-ignored `private/` app and engine logs. It does not
authorize another observation.

```sh
ruby verify_contract.rb
dotnet run --project ../../tools/Gridworks.Scope1Checks/Gridworks.Scope1Checks.csproj -c Release -- ../../data/scope-1-v1.json
```

Do not delete `private/` recursively.
