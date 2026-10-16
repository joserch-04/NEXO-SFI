-- Una cuenta de demostración sin hash no puede activarse accidentalmente.
ALTER TABLE usuarios ADD CONSTRAINT ck_usuario_hash_activo
 CHECK (activo = FALSE OR CHAR_LENGTH(TRIM(password_hash)) > 0);
