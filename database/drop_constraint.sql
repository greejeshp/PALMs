DECLARE @constraint NVARCHAR(200);
SELECT @constraint = name 
FROM sys.check_constraints 
WHERE parent_object_id = OBJECT_ID('ApplicationDocuments') 
  AND definition LIKE '%DocType%';

IF @constraint IS NOT NULL 
BEGIN 
    EXEC('ALTER TABLE ApplicationDocuments DROP CONSTRAINT ' + @constraint); 
    PRINT 'Dropped constraint: ' + @constraint; 
END 
ELSE 
BEGIN
    PRINT 'Constraint not found.';
END
