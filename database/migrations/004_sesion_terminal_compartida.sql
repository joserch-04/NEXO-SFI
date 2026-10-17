-- No reutilizar sesiones persistentes anteriores a la política de terminal compartida.
ALTER TABLE sesiones_refresh ADD COLUMN ultima_actividad DATETIME(6) NULL;
UPDATE sesiones_refresh SET ultima_actividad=creado_en,
    revocado_en=COALESCE(revocado_en,UTC_TIMESTAMP(6));
ALTER TABLE sesiones_refresh MODIFY ultima_actividad DATETIME(6) NOT NULL;
