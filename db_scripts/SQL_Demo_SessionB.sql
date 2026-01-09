--- Session B ---

-- ### Dirty Read - Read uncommitted changes ###
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;
SELECT Balance FROM Accounts WHERE AccountID = 1;
-- ### END ###

-- ### Non-repeatable Read - Read changed data in same transaction ###
UPDATE Accounts SET Balance = 200 WHERE AccountID = 1;
-- ### END ###;

-- ### Prevent Non-repeatable - Block updates during read ###
UPDATE Accounts SET Balance = 200 WHERE AccountID = 1;
-- (Try to commit)
-- ### END ###

-- ### Phantom Read - New rows appear between reads ###
BEGIN TRAN;
SET IDENTITY_INSERT Orders ON;
INSERT INTO Orders (OrderID, Customer) VALUES (4, 'Ivan');
COMMIT;
SET IDENTITY_INSERT Orders OFF;
-- ### END ###

-- ### Prevent Phantom - Block inserts in range ###
SET IDENTITY_INSERT Orders ON;
INSERT INTO Orders (OrderID, Customer) VALUES (11, 'Phantom');
SET IDENTITY_INSERT Orders OFF;
-- This statement will be BLOCKED until Session A commits or rolls back
-- ### END ###

-- ### Snapshot Isolation - Read consistent snapshot ###
BEGIN TRAN;
UPDATE Accounts SET Balance = 200 WHERE AccountID = 1;
COMMIT;
-- ### END ###














-- ### SERIALIZABLE Isolation: Pessimistic Locking ###
--SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
--BEGIN TRAN;
--SELECT Stock FROM Inventory WHERE ItemID = 1; -- BLOCKED until Session A commits
--UPDATE Inventory SET Stock = Stock - 1 WHERE ItemID = 1;
--COMMIT;
-- ### END ###

-- ### SNAPSHOT Isolation: Optimistic Concurrency ###
--SET TRANSACTION ISOLATION LEVEL SNAPSHOT;
--BEGIN TRAN;
--SELECT Stock FROM Inventory WHERE ItemID = 1; -- Returns 1
--UPDATE Inventory SET Stock = Stock - 1 WHERE ItemID = 1;
--COMMIT;
-- If Session A commits first, Session B's COMMIT will fail with a conflict error
-- Session B must retry the transaction
-- ### END ###