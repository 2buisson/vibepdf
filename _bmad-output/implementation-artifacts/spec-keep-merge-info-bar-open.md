---
title: 'Keep merge info bar open and place it above the action bar'
type: 'bugfix'
created: '2026-06-22'
status: 'done'
route: 'one-shot'
---

# Keep merge info bar open and place it above the action bar

## Intent

**Problem:** After a successful merge the info bar auto-dismissed itself after ~8 seconds, so the confirmation (and its "Open folder" action) could vanish before the user read or acted on it. The info bar also sat at the top of the preview panel, far from the Merge button the user had just pressed.

**Approach:** (1) Remove the one-shot auto-dismiss timer so the success banner stays open until the user closes it (`IsClosable="True"`) or presses Merge again, which already clears any visible banner (AC #11). (2) Reorder the preview panel grid so the preview content fills the top (row 0) and the merge banners (row 1) sit directly above the action bar (row 2). Error banners were already manual-dismiss only and are unchanged.

## Suggested Review Order

**Info bar behavior**

- Core change — drop the `StartSuccessAutoDismiss()` call so the success banner stays open; the field + timer method are removed with it.
  [`MainViewModel.cs:420`](../../pdfjunior/ViewModels/MainViewModel.cs#L420)

**Info bar placement**

- Preview panel rows reordered: content now `*` (row 0), banners + action bar are the trailing `Auto` rows.
  [`MainWindow.xaml:115`](../../pdfjunior/MainWindow.xaml#L115)

- Banner StackPanel moved to row 1 (above the action bar) with padding adjusted to breathe above the Merge button; InfoBar stays `IsClosable="True"`.
  [`MainWindow.xaml:124`](../../pdfjunior/MainWindow.xaml#L124)
