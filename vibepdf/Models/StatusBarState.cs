namespace vibepdf.Models;

// Drives the always-visible bottom status bar. Ready is the idle default (0);
// a merge runs Ready → Merging → Success | Error.
public enum StatusBarState { Ready, Merging, Success, Error }
