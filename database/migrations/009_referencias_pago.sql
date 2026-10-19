-- Las referencias previas se conservan NULL: no inventar identificadores bancarios.
ALTER TABLE pagos ADD referencia VARCHAR(100) NULL,
 ADD CONSTRAINT chk_pago_referencia_efectivo CHECK(metodo<>'EFECTIVO' OR referencia IS NULL);
