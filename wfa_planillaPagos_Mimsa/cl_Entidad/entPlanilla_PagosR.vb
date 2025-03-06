Imports Util

<annEntidad("gmi_plaPagosDetalle2", "id", True, True, False)> _
Public Class entPlanilla_PagosR
    Inherits entComunDetalle

    ' Atributos
    <annAtributo(True, False, "id", "Nro.Planilla", False)>
    Private i_id As Integer
    <annAtributo(True, False, "lineaNumAsg", "Nro.Linea Asig", False)>
    Private i_lineaNumAsg As Integer
    <annAtributo(True, False, "idEC", "Id Estado Cuenta", False)>
    Private i_idEC As Integer
    <annAtributo(True, False, "DocNumSAP", "DocNum SAP", False)>
    Private i_DocNumSAP As Integer
    <annAtributo(True, False, "DocEntrySAP", "DocEntry SAP", False)>
    Private i_DocEntrySAP As Integer
    <annAtributo(True, False, "TransIdSAP", "TransId SAP", False)>
    Private i_TransIdSAP As Integer

    ' Propiedades   
    Public Property id() As Integer
        Get
            Return i_id
        End Get
        Set(ByVal value As Integer)
            i_id = value
        End Set
    End Property

    Public Property lineaNumAsg() As Integer
        Get
            Return i_lineaNumAsg
        End Get
        Set(ByVal value As Integer)
            i_lineaNumAsg = value
        End Set
    End Property

    Public Property idEC() As Integer
        Get
            Return i_idEC
        End Get
        Set(ByVal value As Integer)
            i_idEC = value
        End Set
    End Property

    Public Property DocNumSAP() As Integer
        Get
            Return i_DocNumSAP
        End Get
        Set(ByVal value As Integer)
            i_DocNumSAP = value
        End Set
    End Property

    Public Property DocEntrySAP() As Integer
        Get
            Return i_DocEntrySAP
        End Get
        Set(ByVal value As Integer)
            i_DocEntrySAP = value
        End Set
    End Property

    Public Property TransIdSAP() As Integer
        Get
            Return i_TransIdSAP
        End Get
        Set(ByVal value As Integer)
            i_TransIdSAP = value
        End Set
    End Property

End Class
