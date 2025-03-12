/*<accion>*/create/*</accion>*/ 
/*<tipoSQL>*/procedure/*</tipoSQL>*/ 
/*<nombre>*/gmi_sp_cuentaPerdidaDiferenciaTC/*</nombre>*/



as

select TOP 1 ISNULL( AcctCode,'') AS AcctCode
from OACT
where AcctName LIKE  '%PERDIDA POR DIFERENCIA%'