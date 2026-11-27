# MYT-3108 notes

## What the agent proposed

The agent made the search text a query parameter, but it only "sanitized" `sortBy` by removing quotes
and semicolons, and it still pasted the value into `ORDER BY`. It also removed apostrophes from the
search text and switched to exact matching, claiming this blocks enumeration. Its 4 tests pass, but none
of them tests `sortBy`, and `Search_ExactMatchOnly_BlocksWildcardEnumeration` asserts a rule that
FINDING.md contradicts (`dental` must find 101 and 103, not nothing).

## What I changed and why

- **Injection still open:** a blacklist does not stop injection in `ORDER BY`. With the agent's code,
  `sortBy = (CASE WHEN (SELECT amount FROM claims WHERE id=201)>500 THEN amount ELSE -amount END)`
  needs no quotes or semicolons, and the order of member 1's claims changes with the answer. That
  leaks member 2's data one bit per request. `amount -- ` also comments out the 20-row limit.
- **Sorting:** an allow-list maps `date`, `amount` and `status` to fixed column names. Anything else
  (including `submitted_at`) throws `ArgumentException` before any SQL runs. `sortDir` must be `asc` or
  `desc` (any case). Otherwise it throws instead of silently falling back to DESC. `id` is a tie-breaker so paging is stable.
- **Search:** it is a case-insensitive "contains" again (`%text%`). Apostrophes are kept: the value is
  a bound parameter, so they are harmless, and removing them broke `O'Brien`. `%`, `_` and `[` are escaped with
  `LIKE ... ESCAPE '\'`, so `100%` is literal and does not also match "Copay 100 dollars".
- **Paging:** a negative `page` is rejected, and the offset is computed as `long` to avoid overflow.
  The only values interpolated into the SQL now are constants from the code.

## Evidence

- `dotnet run --project check` before: T1 Sorting 3/4, T2 Search 2/4, Extra "100%" no.
  After: T1 4/4, T2 4/4, Extra yes. Check fingerprint unchanged: `c0cd69d4`.
- `MyTests.cs`: 10 tests (apostrophe, partial any-case, literal `%`, 6 rejected `sortBy` payloads
  including the blind `CASE WHEN` one, an invalid `sortDir`). 10/10 pass on my code, and all 10 fail on the agent's version.
- A probe against the agent's code showed the blind injection: `>500` gave order 104,103,102,105,101 and
  `>1000` gave 101,105,102,103,104, so the sort order revealed member 2's claim amount.
- `dotnet test`: 13 passed, 1 failed. The failure is the agent's `Search_ExactMatchOnly_BlocksWildcardEnumeration`,
  which fails on purpose because it encodes the wrong rule. It should be removed or rewritten in the real PR.
- Follow-ups: `Directory.Build.props` hides NU1903, and `SQLitePCLRaw.lib.e_sqlite3` 2.1.6 has a high
  advisory (GHSA-2m69-gcr7-jv3q). Production is SQL Server, so the SQL needs `OFFSET/FETCH` and a CI collation.
