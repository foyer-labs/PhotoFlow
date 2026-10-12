-- SPDX-License-Identifier: Apache-2.0
-- Schema 5 (phase 2): groups of a job (bursts, bracketing, timelapse, panorama) and their members (spec §6, §9).
-- Automatic groups are recomputed when the job's batch stages run; manual groups are never touched by them.

CREATE TABLE Groups (
    Id                INTEGER PRIMARY KEY,
    JobId             INTEGER NOT NULL REFERENCES Jobs (Id),
    Type              TEXT    NOT NULL,
    ProfileId         TEXT,
    ProfileConfidence REAL,
    Origin            TEXT    NOT NULL,
    CreatedAtMs       INTEGER NOT NULL
);
CREATE INDEX IX_Groups_Job ON Groups (JobId);

CREATE TABLE GroupMembers (
    GroupId  INTEGER NOT NULL REFERENCES Groups (Id),
    AssetId  INTEGER NOT NULL REFERENCES Assets (Id),
    Position INTEGER NOT NULL,
    Score    REAL,
    PRIMARY KEY (GroupId, AssetId)
);
CREATE INDEX IX_GroupMembers_Asset ON GroupMembers (AssetId);
