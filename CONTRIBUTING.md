# Development / Contributing

## Suggested areas to tackle

The best place to look currently would be either:

1. The [issues](https://github.com/icosa-foundation/open-brush/issues)
2. The [Trello cards](https://trello.com/b/jItetqYe/open-brush)
3. Take a look at some of the [experimental branches](https://icosa.gitbook.io/open-brush/alternate-and-experimental-builds) that might need assistance
4. Join the Discord and look at the [feature requests](https://discord.gg/BXYUKzhS)

## Coding Style
While the original Tilt Brush used a Google code style, Open Brush follows standard C# and Python conventions for formatting, indentation, etc. To reduce the work required by contributors, we use the [pre-commit](https://pre-commit.com) python package and git hook to automatically run some syntax checking and formatting prior to commits. Similarly, all Pull Requests use pre-commit to check that these rules are followed. To have your code automatically checked, please run the following (you will need python 3.5+ installed):
```bash
pip install pre-commit # This installs the pre-commit packages.
pre-commit install # This installs the hook in your repo / clone.
dotnet tool install -g dotnet-format # This needs to be installed manually; all other checks will be downloaded automatically.
git commit ...
# If any formatting was done, you'll need to rerun the git commit command with the newly-modified file
```
If you already made any commits without having the pre-commit hook installed, you can manually run the checkers / formatters via `pre-commit run -a`. If any changes are made, please commit them.

If you use Windows, you may want to use [Scoop](https://scoop.sh) to easily install python and dotnet. After installing the Scoop installer, simply run `scoop install dotnet dotnet-sdk python`.

There is also an extensive `.editorconfig` file which will configure many editors' formatting tools properly.

## Git history
As the addition of the coding style above changed almost every single file, you may want to ignore these changes when looking at `git blame`. You can do this by running:
```bash
git config blame.ignoreRevsFile .git-blame-ignore-revs
```
within your clone. The `.git-blame-ignore-revs` file lists the (squashed) commits in which only formatting changes were made, and which should be ignored by git by default. Unfortunately, the Github UI does not support the use of this file.

---

# Tilt Brush CONTRIBUTING

# How to Contribute

We encourage you to fork the repository; this repository does not currently
accept patches or contributions. See README.md for rules governing
the Tilt Brush trademark.

Should our contribution policy ever change, contributors will need to
follow a few small guidelines.

## Contributor License Agreement

Contributions to this project must be accompanied by a Contributor License
Agreement (CLA). You (or your employer) retain the copyright to your
contribution; this simply gives us permission to use and redistribute your
contributions as part of the project. Head over to
<https://cla.developers.google.com/> to see your current agreements on file or
to sign a new one.

You generally only need to submit a CLA once, so if you've already submitted one
(even if it was for a different project), you probably don't need to do it
again.

## Code reviews

All submissions, including submissions by project members, require review. We
use GitHub pull requests for this purpose. Consult
[GitHub Help](https://help.github.com/articles/about-pull-requests/) for more
information on using pull requests.

## Community Guidelines

This project follows
[Google's Open Source Community Guidelines](https://opensource.google/conduct/).
