# Contributing Guidelines

Thank you for showing interest in the development of AX Player. We aim to provide a good collaborating environment for everyone involved, and as such have decided to list some of the most important things to keep in mind in the process. The guidelines below have been chosen based on past experience.

## Table of contents

1. [Reporting bugs](#reporting-bugs)
2. [Providing general feedback](#providing-general-feedback)
3. [Issue or discussion?](#issue-or-discussion)
4. [Submitting pull requests](#submitting-pull-requests)
5. [Resources](#resources)

## Reporting bugs

A **bug** is a situation in which there is something clearly *and objectively* wrong with the app. Examples of applicable bug reports are:

- The app crashes to desktop when I play a video
- The app not playing VP9/AV01 codec videos
- A piece of text is overlapping another piece of text on the screen

To track bug reports, we primarily use GitHub **issues**. When opening an issue, please keep in mind the following:

- Before opening the issue, please search for any similar existing issues using the text search bar and the issue labels. This includes both open and closed issues (we may have already fixed something, but the fix hasn't yet been released).
- When opening the issue, please fill out as much of the issue template as you can. In particular, please make sure to include logs and screenshots as much as possible. The instructions on how to find the log files are included in the issue template.
- We may ask you for follow-up information to reproduce or debug the problem. Please look out for this and provide follow-up info if we request it.

If we cannot reproduce the issue, it is deemed low priority, or it is deemed to be specific to your setup in some way, the issue may be downgraded to a discussion. This will be done by a maintainer for you.

## Providing general feedback

If you wish to:

- provide *subjective* feedback on the app (about how the UI looks, about app mechanics, etc.),
- suggest a new feature to be added to the app,
- report a non-specific problem with the app that you think may be connected to your hardware or operating system specifically,

then it is generally best to start with a **discussion** first. Discussions are a good avenue to group subjective feedback on a single topic, or gauge interest in a particular feature request.

When opening a discussion, please keep in mind the following:

- Use the search function to see if your idea has been proposed before, or if there is already a thread about a particular issue you wish to raise.
- If proposing a feature, please try to explain the feature in as much detail as possible.
- If you're reporting a non-specific problem, please provide applicable logs, screenshots, or video that illustrate the issue.

If a discussion gathers enough traction, then it may be converted into an issue. This will be done by a maintainer for you.

## Issue or discussion?

We realise that the line between an issue and a discussion may be fuzzy, so while we ask you to use your best judgement based on the description above, please don't think about it too hard either. Feedback in a slightly wrong place is better than no feedback at all.

When in doubt, it's probably best to start with a discussion first. We will escalate to issues as needed.

## Submitting pull requests

While pull requests from unaffiliated contributors are welcome, Reviewing PRs that fall outside of the scope of the roadmap is done on a best-effort basis, so please be aware that it may take a while before a core maintainer gets around to review your change.

The [issue tracker](https://github.com/ZeroMayo/AX-Player/issues) should provide plenty of issues to start with. In the case of simple issues, a direct PR is okay. However, if you decide to work on an existing issue which doesn't seem trivial, **please ask us first**. This way we can try to estimate if it is a good fit for you and provide the correct direction on how to address it. In addition, note that while we do not rule out external contributors from working on roadmapped issues, we will generally prefer to handle them ourselves unless they're not very time sensitive.

If you'd like to propose a subjective change to one of the visual aspects of the app, or there is a bigger task you'd like to work on, but there is no corresponding issue or discussion thread yet for it, **please open a discussion or issue first** to avoid wasted effort.

Aside from the above, below is a brief checklist of things to watch out when you're preparing your code changes:

- Make sure you're comfortable with the principles of object-oriented programming, the syntax of C\# and your development environment.
- Make sure you are familiar with [git](https://git-scm.com/) and [the pull request workflow](https://help.github.com/en/github/collaborating-with-issues-and-pull-requests/proposing-changes-to-your-work-with-pull-requests).
- Please do not make code changes via the GitHub web interface.
- Please add tests for your changes. We expect most new features and bugfixes to have test coverage, unless the effort of adding them is prohibitive. The visual testing methodology we use is described in more detail [here](https://github.com/ppy/osu-framework/wiki/Development-and-Testing).
- Please run tests and code style analysis (via `InspectCode.{ps1,sh}` scripts in the root of this repository) before opening the PR. This is particularly important if you're a first-time contributor, as CI will not run for your PR until we allow it to do so.

After you're done with your changes and you wish to open the PR, please observe the following recommendations:

- Please submit the pull request from a [topic branch](https://git-scm.com/book/en/v2/Git-Branching-Branching-Workflows#_topic_branch) (not `master`), and keep the *Allow edits from maintainers* check box selected, so that we can push fixes to your PR if necessary.
- Please avoid pushing untested or incomplete code.
- Please do not force-push or rebase unless we ask you to.
- Please do not merge `master` continually if there are no conflicts to resolve. We will do this for you when the change is ready for merge.

We are highly committed to quality when it comes to the AX Player project. This means that contributions from less experienced community members can take multiple rounds of review to get to a mergeable state. We try our utmost best to never conflate a person with the code they authored, and to keep the discussion focused on the code at all times. Please consider our comments and requests a learning experience.

If you're uncertain about some part of the codebase or some inner workings of the app and framework, please reach out either by leaving a comment in the relevant issue, discussion, or PR thread. We will try to help you as much as we can.

## Resources

- [`ppy/osu-framework` wiki](https://github.com/ppy/osu-framework/wiki): Contains introductory information about osu!framework, the bespoke 2D game framework we use for the app