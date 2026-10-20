# Parked CI workflow

`ci.yml` lives here instead of `.github/workflows/` because the push token used by the
orchestration session lacks the `workflow` scope. To activate it:

```
gh auth refresh -h github.com -s workflow
git mv .github/workflows-pending/ci.yml .github/workflows/ci.yml
```
