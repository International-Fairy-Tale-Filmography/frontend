# IFTF Data Editor

The data editor provides table-style access to the IFTF filmography data.

## Editing data

- Choose a data set at the top of the editor, then select a table to browse or edit records.
- **Save changes** keeps your edits in memory in the current tab; they are not published yet. The save count tracks Save actions, not unique records. If you reload or close the tab before publishing, those edits are lost.
- **Review and publish changes** shows changed records and fields before publishing them together.
- Film, company, country, language, origin, person, and role records can each include a contribution history. Add freeform rows for who contributed, what they did, and when; this is separate from the existing Added/Updated tracking details and from the film cast/crew list.
- If someone else updates the selected data set while you are editing, publishing is paused. Compare the newer data with the edits still open in this tab, then reload the latest data and re-enter any changes you still need.
- Data sets are selected independently per tab. A tab will not overwrite a newer version; reload or compare it before publishing.

## Connecting to the editor

Each user connects with their own fine-grained GitHub personal access token. Limit it to the `International-Fairy-Tale-Filmography/data` repository and grant only **Contents: Read and write** (GitHub adds the required metadata access). Set an expiration date. Organization approval may be required.

The token is kept in the current browser tab's session storage, migrated out of older persistent browser storage when found, and removed when you disconnect or close the tab. Connect separately in each tab. A browser token remains accessible to scripts running in this site, so use the editor only on a trusted device and never share your token.