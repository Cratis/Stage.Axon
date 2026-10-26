# Corpus mapping

Vendored Screenplay models for the verification corpus, and where each one comes from. `Cratis.Screenplay.CanonicalCorpus` is pinned to **4.241.0**. Version 4.250.0 adds no coverage (the only new resources, `AttachedCommandHandlers/{file,inline}.play`, match no vendored model), so the pin stays.

## Sourced from the package

Selected from `ScreenCompositionCorpus.V1.SourceForms.Single(form => form.Name == "folder")` by stable key and written byte for byte into the `sample-custom-packages` input by `CanonicalCorpusInputs`. Specs pin the display path and SHA256 of each, so a pin bump that changes them fails visibly.

| Stable key | Display path |
| --- | --- |
| `workspaces-tracking-feature` | `Workspaces/Tracking/Tracking.play` |
| `add-comment-slice` | `Workspaces/Tracking/AddComment/AddComment.play` |
| `close-work-item-slice` | `Workspaces/Tracking/CloseWorkItem/CloseWorkItem.play` |
| `rename-work-item-slice` | `Workspaces/Tracking/RenameWorkItem/RenameWorkItem.play` |
| `work-item-comments-slice` | `Workspaces/Tracking/WorkItemComments/WorkItemComments.play` |
| `work-item-details-slice` | `Workspaces/Tracking/WorkItemDetails/WorkItemDetails.play` |

## Retained: near-equivalent, different on purpose

Replacing any of these changes the `sample-custom-packages` application.

| File under `live-custom-packages/model` | Difference from the canonical document |
| --- | --- |
| `application.play` | Canonical `application` declares `category Shell` and `root template AppShell`; this file uses `layout AppShell` and declares the `Acme.Charts` package. |
| `Workspaces/Workspaces.play` | Canonical `workspaces-module` adds `category Workspace`/`Dialog`, `generation manual`, a `notes` field and a two-column form layout. |
| `Workspaces/Tracking/CreateWorkItem/CreateWorkItem.play` | Canonical `create-work-item-slice` adds a `notes String` field and the "Two column form" value. |
| `Workspaces/Tracking/WorkItemList/WorkItemList.play` | Has the `Acme.Charts.chart salesChart` custom-package component, the reason this sample exists; the canonical file does not. |

## Retained: no canonical equivalent

| Path | Reason |
| --- | --- |
| `live-blueprints/models/*/*.play` (15 blueprints) | No package resource or selector exists. |
| `live-navigation/model/application.play` | ScreenComposition has only negative `.play.txt` fragments for navigation. |
| `blueprint-templates/model/templates.play` | ScreenComposition has only negative `.play.txt` fragments for templates. |
| `axon-samples` | Kept by the rule in Cratis/Stage.Axon#5. |
