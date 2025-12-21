--- Session A ---

-- ### Dirty Read - Read uncommitted changes ###
SELECT Balance FROM Accounts WHERE AccountID = 1;
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;
BEGIN TRAN;
SELECT Balance FROM Accounts WHERE AccountID = 1;
UPDATE Accounts SET Balance = 100 WHERE AccountID = 1;
WAITFOR DELAY '00:00:10';
-- Keep this transaction open
SELECT Balance FROM Accounts WHERE AccountID = 1;

-- After session B was executed
ROLLBACK;
SELECT Balance FROM Accounts WHERE AccountID = 1;
--COMMIT;
-- ### END ###

-- ### Non-repeatable Read - Read changed data in same transaction ###
SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
BEGIN TRAN;
SELECT Balance FROM Accounts WHERE AccountID = 1;
WAITFOR DELAY '00:00:10';
-- Open another session and run update, then return here
SELECT Balance FROM Accounts WHERE AccountID = 1; -- Value is now 200
COMMIT;
-- ### END ###

-- ### Prevent Non-repeatable - Block updates during read ###
SET TRANSACTION ISOLATION LEVEL REPEATABLE READ;
BEGIN TRAN;
SELECT Balance FROM Accounts WHERE AccountID = 1;
-- (Do not commit or rollback yet) RUN B SESSION
COMMIT; --NOW GO TO SESSION B: UPDATE will finish.
SELECT Balance FROM Accounts WHERE AccountID = 1;
-- ### END ###

-- ### Phantom Read - New rows appear between reads ###
SET TRANSACTION ISOLATION LEVEL REPEATABLE READ;
BEGIN TRAN;
SELECT COUNT(*) FROM Orders;
WAITFOR DELAY '00:00:10';
-- Session B inserts a new row into Orders
-- Session A repeats the SELECT and sees a new row => Phantom Read
SELECT COUNT(*) FROM Orders;
COMMIT
-- ### END ###

-- ### Prevent Phantom - New rows appear between reads ###
SET TRANSACTION ISOLATION LEVEL REPEATABLE READ;
BEGIN TRAN;
SELECT COUNT(*) FROM Orders;
WAITFOR DELAY '00:00:10'; -- Keeps transaction open for 10 seconds
SELECT COUNT(*) FROM Orders;
COMMIT;
-- ### END ###

-- ### Prevent Phantom - Block inserts in range ###
SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
BEGIN TRAN;
SELECT COUNT(*) AS 'BEFORE_INSERT_WAS_COMITTED_ORDERS_COUNT' FROM Orders;
-- Do not commit or rollback yet; keep the transaction open
WAITFOR DELAY '00:00:10'
SELECT COUNT(*) AS 'AFTER_DELAY_BEFORE_INSERT_WAS_COMITTED_ORDERS_COUNT' FROM Orders;
COMMIT; -- WHEN YOU DO THIS INSERT will complete on session 'B'
SELECT COUNT(*)  AS 'AFTER_INSERT_WAS_COMITTED_ORDERS_COUNT'  FROM Orders;
WAITFOR DELAY '00:00:05'
-- ### END ###

-- ### Snapshot Isolation - Read consistent snapshot ###
ALTER DATABASE IsolationDemo SET ALLOW_SNAPSHOT_ISOLATION ON; -- By default, SQL Server databases do not have snapshot isolation enabled.
SET TRANSACTION ISOLATION LEVEL SNAPSHOT;
BEGIN TRAN;
SELECT Balance FROM Accounts WHERE AccountID = 1;
WAITFOR DELAY '00:00:05';
-- GO TO Session B 
SELECT Balance FROM Accounts WHERE AccountID = 1;
-- Snapshot provides consistent snapshot view
ALTER DATABASE IsolationDemo SET ALLOW_SNAPSHOT_ISOLATION OFF;
--- ### END ###



















-- ### SERIALIZABLE Isolation: Pessimistic Locking ###
--SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
--BEGIN TRAN;
--SELECT Stock FROM Inventory WHERE ItemID = 1; -- Returns 1
---- Simulate some processing time
--WAITFOR DELAY '00:00:10';
--UPDATE Inventory SET Stock = Stock - 1 WHERE ItemID = 1;
--COMMIT;
-- ### END ###

-- ### SNAPSHOT Isolation: Optimistic Concurrency ###
--ALTER DATABASE YourDatabase SET ALLOW_SNAPSHOT_ISOLATION ON;
--SET TRANSACTION ISOLATION LEVEL SNAPSHOT;
--BEGIN TRAN;
--SELECT Stock FROM Inventory WHERE ItemID = 1; -- Returns 1
--WAITFOR DELAY '00:00:10';
--UPDATE Inventory SET Stock = Stock - 1 WHERE ItemID = 1;
--COMMIT;
-- ### END ###
