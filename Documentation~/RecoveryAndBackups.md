# Recovery And Backups

Writes use this order:

1. Serialize and validate before touching the current primary.
2. Delete abandoned temporary files for the same document.
3. Write a uniquely named temporary file in the destination directory.
4. Flush and close the temporary file.
5. Move the current primary to a newest backup when backup retention is greater than zero.
6. Move the temporary file into the primary location.
7. If replacement fails after primary rotation, restore the newest backup to primary.
8. Delete backups beyond configured retention.

Backups are named deterministically with document stem, timestamp, and sequence. Recovery tries primary first and then backups newest to oldest.

The package does not silently overwrite corrupted saves during load. It returns an explicit failure or recovered result and leaves evidence in storage according to the write/cleanup policy.
