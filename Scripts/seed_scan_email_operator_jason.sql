-- One-time seed: operators for shared login (carousel when 2+ rows).
-- Run against your Firebird database after SCAN_EMAIL_OPERATOR exists (app startup creates it).
-- Uncomment the next line to replace any existing rows for this email, then run the INSERTs.
-- DELETE FROM SCAN_EMAIL_OPERATOR WHERE UPPER(TRIM(EMAIL)) = UPPER('jason.choo2004@gmail.com');

INSERT INTO SCAN_EMAIL_OPERATOR (EMAIL, DISPLAY_NAME, SORT_ORDER, ISACTIVE)
VALUES ('jason.choo2004@gmail.com', 'Jason Choo', 1, 'Y');

INSERT INTO SCAN_EMAIL_OPERATOR (EMAIL, DISPLAY_NAME, SORT_ORDER, ISACTIVE)
VALUES ('jason.choo2004@gmail.com', 'Ahmad - Warehouse', 2, 'Y');

INSERT INTO SCAN_EMAIL_OPERATOR (EMAIL, DISPLAY_NAME, SORT_ORDER, ISACTIVE)
VALUES ('jason.choo2004@gmail.com', 'Siti - Receiving', 3, 'Y');

INSERT INTO SCAN_EMAIL_OPERATOR (EMAIL, DISPLAY_NAME, SORT_ORDER, ISACTIVE)
VALUES ('jason.choo2004@gmail.com', 'Wei Ling', 4, 'Y');

INSERT INTO SCAN_EMAIL_OPERATOR (EMAIL, DISPLAY_NAME, SORT_ORDER, ISACTIVE)
VALUES ('jason.choo2004@gmail.com', 'Kumar - Night shift', 5, 'Y');
