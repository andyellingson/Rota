// MongoDB migration: Shift -> WorkItem
// Run with mongosh against the Rota database, e.g.:
//   mongosh "<connection-string>/<database>" --file migrate-shifts-to-workitems.js
//
// Steps (idempotent - safe to re-run):
//   1. Copy every document from the "shifts" collection into "workItems" using an aggregation $merge.
//   2. Rename the users.shiftAssignmentPriority field to workItemAssignmentPriority.
//   3. Verify counts, then (manually) drop the legacy "shifts" collection once verified.
//
// Take a backup (mongodump) before running.

// ---------------------------------------------------------------------------
// 1. shifts -> workItems (documents keep their _id, so re-running does not duplicate)
// ---------------------------------------------------------------------------
if (db.getCollectionNames().includes("shifts")) {
  db.shifts.aggregate([
    {
      $merge: {
        into: "workItems",
        on: "_id",
        whenMatched: "replace",
        whenNotMatched: "insert"
      }
    }
  ]);
}

// ---------------------------------------------------------------------------
// 2. users.shiftAssignmentPriority -> users.workItemAssignmentPriority
//    Pipeline-style update copies the value across and removes the old field.
// ---------------------------------------------------------------------------
db.users.updateMany(
  { shiftAssignmentPriority: { $exists: true } },
  [
    {
      $set: {
        workItemAssignmentPriority: {
          $ifNull: ["$workItemAssignmentPriority", "$shiftAssignmentPriority"]
        }
      }
    },
    { $unset: "shiftAssignmentPriority" }
  ]
);

// ---------------------------------------------------------------------------
// 3. Legacy field cleanup in other collections.
//    Rotation documents previously ignored a legacy 'shifts' element; remove it if present.
// ---------------------------------------------------------------------------
db.rotations.updateMany(
  { shifts: { $exists: true } },
  { $unset: { shifts: "" } }
);

// ---------------------------------------------------------------------------
// Verification
// ---------------------------------------------------------------------------
const legacyCount = db.getCollectionNames().includes("shifts") ? db.shifts.countDocuments({}) : 0;
print("shifts (legacy) count:    " + legacyCount);
print("workItems count:          " + db.workItems.countDocuments({}));
print("users still using legacy: " + db.users.countDocuments({ shiftAssignmentPriority: { $exists: true } }));

// When the counts match and the app is verified, remove the legacy collection:
//   db.shifts.drop();
