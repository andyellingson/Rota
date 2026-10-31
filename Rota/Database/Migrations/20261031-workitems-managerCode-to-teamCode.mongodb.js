// Migration: Rename WorkItem field managerCode -> teamCode using aggregation pipelines.
// Run in mongosh against the application database.

// 1) Preview impacted documents before migrating.
db.workItems.aggregate([
  { $match: { managerCode: { $exists: true } } },
  {
    $project: {
      _id: 1,
      managerCode: 1,
      teamCode: 1
    }
  },
  { $limit: 50 }
]);

// 2) Migrate data with an aggregation update pipeline.
//    - Copies managerCode to teamCode
//    - Removes managerCode
//    - Keeps existing teamCode values when already present
db.workItems.updateMany(
  { managerCode: { $exists: true } },
  [
    {
      $set: {
        teamCode: { $ifNull: ["$teamCode", "$managerCode"] }
      }
    },
    {
      $unset: "managerCode"
    }
  ]
);

// 3) Verify migration results.
db.workItems.aggregate([
  {
    $group: {
      _id: null,
      remainingManagerCode: {
        $sum: {
          $cond: [{ $ifNull: ["$managerCode", false] }, 1, 0]
        }
      },
      migratedTeamCode: {
        $sum: {
          $cond: [{ $ifNull: ["$teamCode", false] }, 1, 0]
        }
      }
    }
  }
]);

// 4) Optional index migration (safe to run repeatedly).
db.workItems.dropIndex("managerCode_1");
db.workItems.createIndex({ teamCode: 1 });
