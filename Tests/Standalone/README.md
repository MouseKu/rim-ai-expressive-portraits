# Standalone checks

Run from the repository root with Python 3:

```powershell
python -B -m unittest discover -s Tests/Standalone -p "test_*.py" -v
```

These checks use only the Python standard library. They validate packaged JSON,
translation keys and placeholders, literal localization keys used by C# code,
provider-specific portrait-state prompt files, image captions,
and required help/frame assets. They do not start RimWorld, contact an AI provider,
or validate Unity rendering and HTTP behavior. Continue to run the Release build
for C# changes.
