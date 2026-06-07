MVPv1.5.0:
- Throttled FileSender for equal bandwidth
- Smarter Bootstrap
    - Fetch missing/broken files instead of everything
    - Script generation workaround to manifest embedding to executable
	- tinydl.exe deprecate
	- bootstrap loop bug
		- fallback for unresolved 3rd-party dependency links
		- fix for non-english character issues in the fullpath
- Don't save when settings don't change