﻿Imports System.Windows.Forms
Imports Util
Imports cl_Entidad
Imports DevExpress.XtraEditors
Imports SAPbobsCOM
Imports System.IO
Imports OfficeOpenXml
Imports Microsoft.VisualBasic.CompilerServices

Public Class classProcesarPlanilla
    Inherits classComun

#Region "Constructor"

    Public Sub New(ByVal po_form As frmComun)
        MyBase.New(po_form)
    End Sub

#End Region

#Region "Inicializacion"

    Public Overrides Sub sub_cargarForm()
        Try

            ' Se asigna el modo del formulario
            sub_asignarModo(enm_modoForm.BUSCAR)

        Catch ex As Exception
            sub_mostrarMensaje(ex.Message, System.Reflection.Assembly.GetExecutingAssembly.GetName.Name, Me.GetType.Name.ToString, System.Reflection.MethodInfo.GetCurrentMethod.Name, enm_tipoMsj.error_exc)
        End Try
    End Sub

#End Region

#Region "ProcesoNegocio"

    Public Sub sub_procesar()
        Try

            ' Se verifica el modo del formulario
            If o_form.Modo = enm_modoForm.BUSCAR Then
                MsgBox("Primero, debe realizar la busqueda de una planilla abierta.")
                Exit Sub
            End If

            ' Se obtiene el estado del documento
            Dim ls_estado As String = str_obtEstadoObjeto()

            ' Se verifica el estado
            If ls_estado <> "O" Then
                MsgBox("Solo se puede procesar planillas abiertas.")
                Exit Sub
            End If

            ' Se muestra un mensaje de confirmacion
            Dim li_confirm As Integer = MessageBox.Show("El proceso creará los Pagos Recibidos del detalle de la planilla en SAP Business One. ¿Está seguro que desea realizar esta acción?", "caption", MessageBoxButtons.YesNoCancel)

            ' Se verifica el resultado del mensaje de confirmacion
            If Not li_confirm = DialogResult.Yes Then
                Exit Sub
            End If

            ' Se procesa la planilla
            sub_procesarPlanilla()

        Catch ex As Exception
            sub_mostrarMensaje(ex.Message, System.Reflection.Assembly.GetExecutingAssembly.GetName.Name, Me.GetType.Name.ToString, System.Reflection.MethodInfo.GetCurrentMethod.Name, enm_tipoMsj.error_exc)
        End Try
    End Sub

    Private Sub sub_procesarPlanilla()
        Try

            ' Se declara una variable para el resultado de las operaciones
            Dim li_resultado As Integer = 0

            ' Se obtiene la entidad asociada al formulario
            Dim lo_planilla As entPlanilla = obj_obtenerEntidad()

            ' Se obtiene el control con el Id del objeto
            Dim lo_txt As TextEdit = ctr_obtenerControl("id", o_form.Controls)

            ' Se verifica si se obtuvo el control
            If lo_txt Is Nothing Then
                sub_mostrarMensaje("No se obtuvo el control con el nombre <id>.", System.Reflection.Assembly.GetExecutingAssembly.GetName.Name, Me.GetType.Name.ToString, System.Reflection.MethodInfo.GetCurrentMethod.Name, enm_tipoMsj.error_sis)
                Exit Sub
            End If

            ' Se obtiene el id desde el control
            Dim li_id As Integer = obj_obtValorControl(lo_txt)

            ' Se obtiene la entidad por codigo
            lo_planilla = lo_planilla.obj_obtPorCodigo(li_id)

            ' Se verifica si el dataTable tiene filas
            If lo_planilla.Lineas.int_contar < 1 Then
                Exit Sub
            End If

            ' Se realiza la conexion a SAP Business One
            Dim lo_SBOCompany As SAPbobsCOM.Company = entComun.sbo_conectar(s_SAPUser, s_SAPPass)

            ' Se verifica si se realizo la conexion hacia SAP Business One
            If lo_SBOCompany Is Nothing Then
                sub_mostrarMensaje("No se realizó la conexión a SAP Business One.", System.Reflection.Assembly.GetExecutingAssembly.GetName.Name, Me.GetType.Name.ToString, System.Reflection.MethodInfo.GetCurrentMethod.Name, enm_tipoMsj.error_sis)
                Exit Sub
            End If

            ' Se inicia la transaccion de SAP Business One
            If bol_iniciarTransSBO(lo_SBOCompany) = False Then
                Exit Sub
            End If

            ' Se declara una variable para obtener el numero de asignaciones
            Dim li_nroAsigs As Integer = entPlanilla.int_obtCantLineasAsgPll(lo_planilla.id)

            ' Se declara una variable para el contador del progreso
            Dim li_contProgreso As Integer = 0

            ' Se obtiene el progressBar asociado al proceso
            Dim lo_progressBar As System.Windows.Forms.ProgressBar = ctr_obtenerControl("progresoPlanilla", o_form.Controls)

            ' Se verifica si se obtuvo el progressBar
            If Not lo_progressBar Is Nothing Then
                lo_progressBar.Maximum = li_nroAsigs
                lo_progressBar.Minimum = 0
            End If

            ' Se declara una variable para el numero de linea de la asignacion del detalle
            Dim li_lineaNumAsg As Integer = -1

            ' Se declara una variable para el tipo de asignacion del registro del detalle de la planilla
            Dim li_tipoAsg As Integer = -1

            ' Se declara una lista para los detalles de la planilla que formen parte de asignaciones de Uno a Muchos o de Muchos a Uno
            Dim lo_lstPlaDet As New List(Of entPlanilla_Lineas)

            ' Se limpia el detalle de los Pagos Recibidos generados desde la planilla
            lo_planilla.PagosR.sub_limpiar()

            ' Se recorre el detalle de la planilla
            For Each lo_planillaDet As entPlanilla_Lineas In lo_planilla.Lineas.lstObjs

                ' Se verifica el numero de asignacion de linea 
                If li_lineaNumAsg <> lo_planillaDet.LineaNumAsg Then ' La diferencia indica que se trata de una nueva asignacion, por lo tanto se reinicializa el objeto

                    ' El proceso de adicion de MUCHOS A UNO o de UNO a MUCHOS se realiza en el objeto siguiente 
                    ', pues, al notar un cambio en el numero de asignacion, el proceso debe ingresar los detalles obtenidos en el listado llenado hasta el objeto anterior del FOR EACH
                    If li_tipoAsg = 1 Or li_tipoAsg = 2 Then

                        ' Se realiza la inserción UNO a MUCHOS o de MUCHOS A UNO
                        li_resultado = int_procesarMuchosAMuchos(lo_lstPlaDet, lo_SBOCompany, lo_planilla, li_tipoAsg)

                        ' Se verifica el resultado 
                        If li_resultado <> 0 Then

                            ' Se revierte la transaccion
                            bol_RollBackTransSBO(lo_SBOCompany)

                            ' Se desconecta la compañia 
                            lo_SBOCompany.Disconnect()

                            ' Se resetea el progressBar
                            sub_resetProgressBar(lo_progressBar)

                            ' Se finaliza el metodo
                            Exit Sub

                        End If

                        ' Se incrementa el valor del progressBar
                        sub_incrementarProgressBar(lo_progressBar)

                        ' Se limpia el listado de objetos de detalle 
                        lo_lstPlaDet.Clear()

                    End If

                    ' Se obtiene el numero de linea de la asignacion del detalle
                    li_lineaNumAsg = lo_planillaDet.LineaNumAsg

                    ' Se verifica el tipo de asignacion del registro del detalle de la planilla
                    li_tipoAsg = lo_planillaDet.tipoAsg

                    ' Se verifica el tipo de asignación para generar el pago
                    If li_tipoAsg = 0 Then ' Asignacion de UNO a UNO

                        ' Se realiza la adición del objeto
                        li_resultado = int_procesarUnoAUno(lo_planillaDet, lo_SBOCompany, lo_planilla)

                        ' Se verifica el resultado 
                        If li_resultado <> 0 Then

                            ' Se revierte la transaccion
                            bol_RollBackTransSBO(lo_SBOCompany)

                            ' Se desconecta la compañia 
                            lo_SBOCompany.Disconnect()

                            ' Se resetea el progressBar
                            sub_resetProgressBar(lo_progressBar)

                            ' Se finaliza el metodo
                            Exit Sub

                        End If

                        ' Se incrementa el valor del progressBar
                        sub_incrementarProgressBar(lo_progressBar)
                        li_contProgreso = li_contProgreso + 1

                    ElseIf li_tipoAsg = 1 Or li_tipoAsg = 2 Then ' Asignacion de UNO a MUCHOS o MUCHOS a UNO

                        ' Se añade el detalle de la planilla al listado
                        lo_lstPlaDet.Add(lo_planillaDet)

                    Else

                        ' Hubo un error al momento de generar el detalle de la planilla
                        sub_errorRegistroPlanilla(lo_SBOCompany)

                        ' Se revierte la transaccion
                        bol_RollBackTransSBO(lo_SBOCompany)

                        ' Se desconecta la compañia 
                        lo_SBOCompany.Disconnect()

                        ' Se resetea el progressBar
                        sub_resetProgressBar(lo_progressBar)

                        ' Se termina el metodo
                        Exit Sub

                    End If

                Else

                    ' Se verifica si el tipo de asignacion es igual al anterior
                    If li_tipoAsg <> lo_planillaDet.tipoAsg Or li_tipoAsg = 0 Then

                        ' Hubo un error al momento de generar el detalle de la planilla
                        sub_errorRegistroPlanilla(lo_SBOCompany)

                        ' Se desconecta la compañia 
                        lo_SBOCompany.Disconnect()

                        ' Se revierte la transaccion
                        bol_RollBackTransSBO(lo_SBOCompany)

                        ' Se resetea el progressBar
                        sub_resetProgressBar(lo_progressBar)

                        ' Se termina el metodo
                        Exit Sub

                    End If

                    ' Se añade el detalle de la planilla al listado
                    lo_lstPlaDet.Add(lo_planillaDet)

                End If

            Next

            ' Se verifica si existe un proceso de UNO a MUCHOS o de MUCHOS a UNO por ejecutar
            If lo_lstPlaDet.Count > 0 Then

                ' Si el listado de objetos de detalle a procesar tiene objetos, quiere decir que está pendiente una ejecución de una asignacion 1 o 2
                If li_tipoAsg = 1 Or li_tipoAsg = 2 Then

                    ' Se realiza la inserción UNO a MUCHOS o de MUCHOS A UNO
                    li_resultado = int_procesarMuchosAMuchos(lo_lstPlaDet, lo_SBOCompany, lo_planilla, li_tipoAsg)

                    ' Se verifica el resultado 
                    If li_resultado <> 0 Then

                        ' Se revierte la transaccion
                        bol_RollBackTransSBO(lo_SBOCompany)

                        ' Se desconecta la compañia 
                        lo_SBOCompany.Disconnect()

                        ' Se resetea el progressBar
                        sub_resetProgressBar(lo_progressBar)

                        ' Se finaliza el metodo
                        Exit Sub

                    End If

                    ' Se incrementa el valor del progressBar
                    sub_incrementarProgressBar(lo_progressBar)

                    ' Se limpia el listado de objetos de detalle 
                    lo_lstPlaDet.Clear()

                End If

            End If

            ' Se verifica el resultado de la operacion 
            If li_resultado <> 0 Then

                ' Se revierte la transaccion
                bol_RollBackTransSBO(lo_SBOCompany)

                ' Se muestra un mensaje que indica que ocurrió un error en el proceso
                sub_mostrarMensaje("Ocurrió un error durante la ejecución del proceso.", System.Reflection.Assembly.GetExecutingAssembly.GetName.Name, Me.GetType.Name.ToString, System.Reflection.MethodInfo.GetCurrentMethod.Name, enm_tipoMsj.error_sis)

                ' Se resetea el progressBar
                sub_resetProgressBar(lo_progressBar)

                ' Se desconecta la compañia 
                lo_SBOCompany.Disconnect()

                ' Se finaliza el metodo
                Exit Sub

            End If

            ' Se confirma la transaccion
            Dim ls_resPla As String = str_CommitTransSBO(lo_SBOCompany)

            ' Se verifica el resultado de la confirmacion de las operaciones en SAP Business One
            If ls_resPla.Trim <> "" Then
                sub_mostrarMensaje("El proceso no creo los Pagos Recibidos en SAP Business One:  " & ls_resPla & "", System.Reflection.Assembly.GetExecutingAssembly.GetName.Name, Me.GetType.Name.ToString, System.Reflection.MethodInfo.GetCurrentMethod.Name, enm_tipoMsj.error_sap)

                ' Se resetea el progressBar
                sub_resetProgressBar(lo_progressBar)

                ' Se desconecta la compañia 
                lo_SBOCompany.Disconnect()

                ' Se finaliza el metodo
                Exit Sub
            End If

            ' Se muestra un mensaje que indica que el proceso se realizó con exito
            MsgBox("El proceso de creación de los Pagos Recibidos en SAP Business One finalizó de manera correcta.")

            ' Se desconecta la compañia 
            lo_SBOCompany.Disconnect()

            ' Se actualiza el objeto de la planilla
            lo_planilla.Estado = "C"
            lo_planilla.FechaPrcs = Now.Date
            ls_resPla = lo_planilla.str_actualizar()

            ' Se verifica el resultado de la actualizacion
            If ls_resPla.Trim <> "" Then

                ' Se muestra un mensaje que indique que no se actualizó los números SAP en el detalle de la planilla
                sub_mostrarMensaje("Ocurrió un error al actualizar el Estado y los Pagos Recibidos asociados a la planilla.", System.Reflection.Assembly.GetExecutingAssembly.GetName.Name, Me.GetType.Name.ToString, System.Reflection.MethodInfo.GetCurrentMethod.Name, enm_tipoMsj.error_sis)

                ' Se resetea el progressBar
                sub_resetProgressBar(lo_progressBar)

                ' Se finaliza el metodo
                Exit Sub

            End If

            ' Se cambia el valor del estado del combo
            sub_asignarEstadoObjeto(lo_planilla.Estado)

            ' Se actualiza los numeros SAP en la tabla de detalle
            ls_resPla = entPlanilla.str_actualizarNrosSAPPlaDet(lo_planilla.id)

            ' Se verifica si se actualizó los números SAP de manera correcta
            If ls_resPla.Trim <> "" Then

                ' Se muestra un mensaje que indique que no se actualizó los números SAP en el detalle de la planilla
                sub_mostrarMensaje("No se actualizó los números SAP de los Pagos Recibidos creados en el detalle de la planilla.", System.Reflection.Assembly.GetExecutingAssembly.GetName.Name, Me.GetType.Name.ToString, System.Reflection.MethodInfo.GetCurrentMethod.Name, enm_tipoMsj.error_sis)

            End If

            ' Se reasigna el modo del formulario a Busqueda
            sub_asignarModo(enm_modoForm.BUSCAR)

            ' Se resetea el progressBar
            sub_resetProgressBar(lo_progressBar)

        Catch ex As Exception
            sub_mostrarMensaje(ex.Message, System.Reflection.Assembly.GetExecutingAssembly.GetName.Name, Me.GetType.Name.ToString, System.Reflection.MethodInfo.GetCurrentMethod.Name, enm_tipoMsj.error_exc)
        End Try
    End Sub

    Private Function int_procesarUnoAUno(ByVal po_planillaDet As entPlanilla_Lineas, ByVal po_SBOCompany As SAPbobsCOM.Company, ByVal po_planilla As entPlanilla) As Integer
        Try

            ' Se declara un objeto de tipo Payment del SDK de SAP Business One
            Dim lo_payment As SAPbobsCOM.Payments

            ' Se inicializa el objeto
            lo_payment = po_SBOCompany.GetBusinessObject(SAPbobsCOM.BoObjectTypes.oIncomingPayments)

            ' Se indica que el pago es de Clientes
            lo_payment.DocType = SAPbobsCOM.BoRcptTypes.rCustomer

            ' Se declara una variable para el resultado de la operacion del objeto de SAP
            Dim li_resultado As Integer = 0
            Dim ls_mensaje As String = ""

            ' Se verifica si la fecha del documento a pagar es mayor a la fecha del pago
            If po_planillaDet.FechaDoc.CompareTo(po_planillaDet.FechaPago) > 0 Then
                If po_planillaDet.FechaPago.Year < Now.Date.Year Then
                    lo_payment.TaxDate = Now.Date 'po_planillaDet.FechaPago
                    lo_payment.DocDate = dte_obtFechaContabPago(po_planillaDet.FechaDoc) ' La asignacion de la fecha de contabilizacion del pago dependerá del estado del periodo
                    lo_payment.DueDate = Now.Date 'po_planillaDet.FechaPago)
                Else
                    lo_payment.TaxDate = po_planillaDet.FechaPago
                    lo_payment.DocDate = dte_obtFechaContabPago(po_planillaDet.FechaDoc) ' La asignacion de la fecha de contabilizacion del pago dependerá del estado del periodo
                    lo_payment.DueDate = po_planillaDet.FechaPago
                End If

            Else
                If po_planillaDet.FechaPago.Year < Now.Date.Year Then
                    lo_payment.TaxDate = Now.Date 'po_planillaDet.FechaPago
                    lo_payment.DocDate = dte_obtFechaContabPago(po_planillaDet.FechaPago) ' La asignacion de la fecha de contabilizacion del pago dependerá del estado del periodo
                    lo_payment.DueDate = Now.Date 'po_planillaDet.FechaPago)
                Else
                    If po_planillaDet.FechaPago.Month < Now.Date.Month Then
                        lo_payment.TaxDate = Now.Date
                        lo_payment.DocDate = dte_obtFechaContabPago(po_planillaDet.FechaPago) ' La asignacion de la fecha de contabilizacion del pago dependerá del estado del periodo
                        lo_payment.DueDate = Now.Date
                    Else
                        lo_payment.TaxDate = po_planillaDet.FechaPago
                        lo_payment.DocDate = dte_obtFechaContabPago(po_planillaDet.FechaPago) ' La asignacion de la fecha de contabilizacion del pago dependerá del estado del periodo
                        lo_payment.DueDate = po_planillaDet.FechaPago
                    End If
                End If

            End If
            'lo_payment.UserFields.Fields.Item("U_BYR_FECDEP").Value = po_planillaDet.FechaDeposito
            If po_SBOCompany.CompanyDB <> "SBO_ComercialMendoza" And po_SBOCompany.CompanyDB <> "Z_MIMSA_19022025" Then
                lo_payment.UserFields.Fields.Item("U_BYR_FECDEP").Value = po_planillaDet.FechaDeposito
            End If
            ' Se asigna las propiedades de la cabecera del objeto Payment
            lo_payment.CardCode = po_planillaDet.Codigo
            lo_payment.Remarks = Mid(po_planilla.Comentario, 1, 254)
            lo_payment.JournalRemarks = Mid(po_planilla.Comentario, 1, 50)
            lo_payment.TransferReference = Mid("Planilla Nro. " & po_planillaDet.id.ToString, 1, 27)
            lo_payment.DocCurrency = po_planillaDet.MonedaPag


            lo_payment.UserFields.Fields.Item("U_BPP_TRAN").Value = "999"
            lo_payment.UserFields.Fields.Item("U_GMI_RENDICION").Value = po_planillaDet.Nro_Operacion
            lo_payment.UserFields.Fields.Item("U_VS_NRO_POS").Value = po_planillaDet.NumPosicion

            ' Se verifica el tipo de planilla para asignar el tipo de cambio
            If lo_payment.DocCurrency <> str_obtMonLocal() Then
                lo_payment.DocRate = po_planillaDet.Tipo_Cambio
            End If

            ' Se asigna las propiedades al objeto de detalle
            lo_payment.Invoices.InvoiceType = po_planillaDet.Tipo_Doc
            lo_payment.Invoices.DocEntry = po_planillaDet.Id_Doc

            ' Se verifica el tipo de transaccion para asignar el ID de la linea
            If lo_payment.Invoices.InvoiceType = BoRcptInvTypes.it_JournalEntry Then ' Si el documento a pagar es un asiento, se debe especificar en que linea del asiento se encuentra el saldo
                lo_payment.Invoices.DocLine = po_planillaDet.DocLine
            End If

            ' Se asigna el importe aplicado
            If po_planillaDet.MonedaDoc = entComun.str_obtMonLocal Then
                lo_payment.Invoices.SumApplied = po_planillaDet.Imp_Aplicado
            Else
                lo_payment.Invoices.AppliedFC = po_planillaDet.Imp_AplicadoME



            End If

            ' Se asigna el monto y la cuenta con la que se realiza el pago
            lo_payment.TransferAccount = po_planillaDet.Cuenta
            'lo_payment.TransferSum = po_planillaDet.MontoOp
            lo_payment.TransferSum = "1856.01"


            lo_payment.SaveXML("C:\Users\programador_2\Documents\SaveXML_PR\pr_1.xml")

            ' Se realiza la inserción del objeto en la base de datos
            li_resultado = lo_payment.Add

            ' Se verifica el resultado
            If li_resultado <> 0 Then

                ' Se obtiene y se muestra un mensaje de error
                sub_errorProcesoSAP(po_SBOCompany)

                ' Se muestra un mensaje con los detalles
                sub_mostrarMensaje("Numero de asignacion: " & po_planillaDet.LineaNumAsg & " - Id del Documento SAP: " & po_planillaDet.Id_Doc & " - Id del Estado de Cuenta: " & po_planillaDet.idEC, System.Reflection.Assembly.GetExecutingAssembly.GetName.Name, Me.GetType.Name.ToString, System.Reflection.MethodInfo.GetCurrentMethod.Name, enm_tipoMsj.error_sis)

            Else ' Si el proceso de adicion a SAP se ejecutó de manera correcta

                ' Se obtiene el docEntry del objeto recien creado
                Dim ls_docEntry As String = po_SBOCompany.GetNewObjectKey

                ' Se obtiene el objeto de configuracion
                Dim lo_entConf As New entConfig
                lo_entConf = lo_entConf.cfg_obtConfiguracionApp

                ' Se verifica si la configuracion de la aplicacion indica que se debe crear los asientos de diferencia de cambio
                If lo_entConf.CreaAsTC.ToLower = "y" Then

                    ' Se verifica si el registro tiene el check de Diferencia de Tipo de Cambio
                    If po_planillaDet.DifTC.ToLower = "y" Then

                        ' Se realiza la creación del asiento de diferencia de tipo de cambio
                        li_resultado = int_crearAsientoTC(po_planillaDet, po_SBOCompany, po_planilla, ls_docEntry)

                    End If

                End If

                ' Se verifica el resultado de la creación del asiento de diferencia de cambio
                If li_resultado = 0 Then

                    ' Se actualiza los DocEntry de los pagos recien ingresados
                    po_planilla.PagosR.id = po_planillaDet.id
                    po_planilla.PagosR.idEC = po_planillaDet.idEC
                    po_planilla.PagosR.lineaNumAsg = po_planillaDet.LineaNumAsg
                    po_planilla.PagosR.DocEntrySAP = ls_docEntry

                    ' Se añade el detalle
                    po_planilla.PagosR.sub_anadir()

                End If

            End If

            ' Se retorna el resultado
            Return li_resultado

        Catch ex As Exception
            sub_mostrarMensaje(ex.Message, System.Reflection.Assembly.GetExecutingAssembly.GetName.Name, Me.GetType.Name.ToString, System.Reflection.MethodInfo.GetCurrentMethod.Name, enm_tipoMsj.error_exc)
            Return -1
        End Try
    End Function

    Private Function int_procesarUnoAMuchos(ByVal po_lstPlanillaDet As List(Of entPlanilla_Lineas), ByVal po_SBOCompany As SAPbobsCOM.Company, ByVal po_planilla As entPlanilla) As Integer
        Try

            ' Se declara una variable para el resultado de la operacion del objeto de SAP
            Dim li_resultado As Integer = 0

            ' Se declara una variable para obtener el codigo del cliente
            Dim ls_codigoCli As String = ""

            ' Se recorre las lineas de detalle de la lista
            For Each lo_planillaDet As entPlanilla_Lineas In po_lstPlanillaDet

                ' Se realiza la operacion por cada objeto de detalle de la lista
                li_resultado = int_procesarUnoAUno(lo_planillaDet, po_SBOCompany, po_planilla)

                ' Se verifica el resultado
                If li_resultado <> 0 Then
                    Return li_resultado
                End If

            Next

            ' Si las operaciones se realizaron con exito
            Return li_resultado

        Catch ex As Exception
            sub_mostrarMensaje(ex.Message, System.Reflection.Assembly.GetExecutingAssembly.GetName.Name, Me.GetType.Name.ToString, System.Reflection.MethodInfo.GetCurrentMethod.Name, enm_tipoMsj.error_exc)
            Return -1
        End Try
    End Function

    Private Function int_procesarMuchosAUno(ByVal po_lstPlanillaDet As List(Of entPlanilla_Lineas), ByVal po_SBOCompany As SAPbobsCOM.Company, ByVal po_planilla As entPlanilla) As Integer
        Try

            ' Se declara una variable para el resultado de la operacion del objeto de SAP
            Dim li_resultado As Integer = 0
            Dim ls_mensaje As String = ""

            ' Se declara una variable para obtener el codigo del cliente
            Dim ls_codigoCli As String = ""

            ' Se declara una variable para obtener el numero de asignacion, el numero de operacion del pago y el Id del registro del Estado de Cuenta
            Dim li_LineaNumAsg As Integer = -1
            Dim ls_nroOperacion As String = ""
            Dim li_idEC As Integer = -1

            ' Se declara un objeto de tipo Payment del SDK de SAP Business One
            Dim lo_payment As SAPbobsCOM.Payments

            ' Se inicializa el objeto
            lo_payment = po_SBOCompany.GetBusinessObject(SAPbobsCOM.BoObjectTypes.oIncomingPayments)

            ' Se indica que el pago es de Clientes
            lo_payment.DocType = SAPbobsCOM.BoRcptTypes.rCustomer

            ' Se recorre las lineas de detalle de la lista
            For Each lo_planillaDet As entPlanilla_Lineas In po_lstPlanillaDet

                ' Se asigna el codigo del cliente a la variable y las propiedades de la cabecera del pago
                If ls_codigoCli = "" Then ' La primera linea del recorrido

                    ' Se asigna el codigo del cliente
                    ls_codigoCli = lo_planillaDet.Codigo

                    ' Se obtiene el numero de asignacion y el numero de operacion del pago
                    li_LineaNumAsg = lo_planillaDet.LineaNumAsg
                    ls_nroOperacion = lo_planillaDet.Nro_Operacion
                    li_idEC = lo_planillaDet.idEC

                    ' Se verifica si la fecha del documento a pagar es mayor a la fecha del pago
                    If lo_planillaDet.FechaDoc.CompareTo(lo_planillaDet.FechaPago) > 0 Then

                        If lo_planillaDet.FechaPago.Year < Now.Date.Year Then
                            lo_payment.TaxDate = Now.Date 'lo_planillaDet.FechaPago
                            lo_payment.DocDate = dte_obtFechaContabPago(lo_planillaDet.FechaDoc) ' La asignacion de la fecha de contabilizacion del pago dependerá del estado del periodo
                            lo_payment.DueDate = Now.Date 'lo_planillaDet.FechaPago
                        Else
                            lo_payment.TaxDate = lo_planillaDet.FechaPago
                            lo_payment.DocDate = dte_obtFechaContabPago(lo_planillaDet.FechaDoc) ' La asignacion de la fecha de contabilizacion del pago dependerá del estado del periodo
                            lo_payment.DueDate = lo_planillaDet.FechaPago
                        End If

                        'lo_payment.DocDate = dte_obtFechaContabPago(lo_planillaDet.FechaDoc) ' La asignacion de la fecha de contabilizacion del pago dependerá del estado del periodo

                    Else
                        If lo_planillaDet.FechaPago.Year < Now.Date.Year Then
                            lo_payment.TaxDate = Now.Date 'lo_planillaDet.FechaPago
                            lo_payment.DocDate = dte_obtFechaContabPago(lo_planillaDet.FechaPago) ' La asignacion de la fecha de contabilizacion del pago dependerá del estado del periodo
                            lo_payment.DueDate = Now.Date 'lo_planillaDet.FechaPago
                        Else
                            If lo_planillaDet.FechaPago.Month < Now.Date.Month Then
                                lo_payment.TaxDate = Now.Date
                                lo_payment.DocDate = dte_obtFechaContabPago(lo_planillaDet.FechaPago) ' La asignacion de la fecha de contabilizacion del pago dependerá del estado del periodo
                                lo_payment.DueDate = Now.Date
                            Else
                                lo_payment.TaxDate = lo_planillaDet.FechaPago
                                lo_payment.DocDate = dte_obtFechaContabPago(lo_planillaDet.FechaPago) ' La asignacion de la fecha de contabilizacion del pago dependerá del estado del periodo
                                lo_payment.DueDate = lo_planillaDet.FechaPago
                            End If

                        End If
                        'lo_payment.DocDate = dte_obtFechaContabPago(lo_planillaDet.FechaPago) ' La asignacion de la fecha de contabilizacion del pago dependerá del estado del periodo

                    End If
                    'lo_payment.UserFields.Fields.Item("U_BYR_FECDEP").Value = lo_planillaDet.FechaDeposito
                    If po_SBOCompany.CompanyDB <> "SBO_ComercialMendoza" Then
                        lo_payment.UserFields.Fields.Item("U_BYR_FECDEP").Value = lo_planillaDet.FechaDeposito
                    End If
                    ' Se asigna las propiedades de la cabecera del objeto Payment
                    lo_payment.CardCode = lo_planillaDet.Codigo
                    lo_payment.DocCurrency = lo_planillaDet.MonedaPag
                    lo_payment.Remarks = Mid(po_planilla.Comentario, 1, 254)
                    lo_payment.JournalRemarks = Mid(po_planilla.Comentario, 1, 50)
                    lo_payment.TransferReference = Mid("Planilla Nro. " & lo_planillaDet.id.ToString, 1, 27)
                    lo_payment.UserFields.Fields.Item("U_BPP_TRAN").Value = "999"
                    lo_payment.UserFields.Fields.Item("U_GMI_RENDICION").Value = lo_planillaDet.Nro_Operacion
                    lo_payment.UserFields.Fields.Item("U_VS_NRO_POS").Value = lo_planillaDet.NumPosicion
                    ' Se verifica el tipo de planilla para asignar el tipo de cambio
                    If lo_payment.DocCurrency <> str_obtMonLocal() Then
                        lo_payment.DocRate = lo_planillaDet.Tipo_Cambio
                        'If po_planilla.TipoPla = "D" Then
                        '    lo_payment.DocRate = lo_planillaDet.TipoCambioDoc
                        'Else
                        '    lo_payment.DocRate = lo_planillaDet.Tipo_Cambio
                        'End If
                    End If

                    ' Se asigna el monto y la cuenta con la que se realiza el pago
                    lo_payment.TransferAccount = lo_planillaDet.Cuenta
                    lo_payment.TransferSum = lo_planillaDet.MontoOp

                End If

                ' Se verifica que los registros tengan el mismo codigo de cliente, pues no es posible aplicar un pago a diferentes clientes
                If ls_codigoCli <> lo_planillaDet.Codigo Then
                    sub_errorRegistroPlanilla(po_SBOCompany)
                    sub_mostrarMensaje("No es posible aplicar un pago a documentos de diferentes clientes", System.Reflection.Assembly.GetExecutingAssembly.GetName.Name, Me.GetType.Name.ToString, System.Reflection.MethodInfo.GetCurrentMethod.Name, enm_tipoMsj.error_sis)
                End If

                ' Se verifica si la fecha del documento a pagar es mayor a la fecha del pago
                If lo_planillaDet.FechaDoc.CompareTo(lo_planillaDet.FechaPago) > 0 Then
                    If lo_planillaDet.FechaPago.Year < Now.Date.Year Then
                        lo_payment.TaxDate = Now.Date 'lo_planillaDet.FechaPago
                        lo_payment.DocDate = dte_obtFechaContabPago(lo_planillaDet.FechaDoc) ' La asignacion de la fecha de contabilizacion del pago dependerá del estado del periodo
                        lo_payment.DueDate = Now.Date 'lo_planillaDet.FechaPago
                    Else
                        If lo_planillaDet.FechaPago.Month < Now.Date.Month Then
                            lo_payment.TaxDate = Now.Date
                            lo_payment.DocDate = dte_obtFechaContabPago(lo_planillaDet.FechaDoc) ' La asignacion de la fecha de contabilizacion del pago dependerá del estado del periodo
                            lo_payment.DueDate = Now.Date
                        Else
                            lo_payment.TaxDate = lo_planillaDet.FechaPago
                            lo_payment.DocDate = dte_obtFechaContabPago(lo_planillaDet.FechaDoc) ' La asignacion de la fecha de contabilizacion del pago dependerá del estado del periodo
                            lo_payment.DueDate = lo_planillaDet.FechaPago
                        End If

                    End If
                    'lo_payment.DocDate = dte_obtFechaContabPago(lo_planillaDet.FechaDoc) ' La asignacion de la fecha de contabilizacion del pago dependerá del estado del periodo

                End If

                ' Se asigna las propiedades al objeto de detalle
                lo_payment.Invoices.InvoiceType = lo_planillaDet.Tipo_Doc
                lo_payment.Invoices.DocEntry = lo_planillaDet.Id_Doc

                ' Se verifica el tipo de transaccion para asignar el ID de la linea
                If lo_payment.Invoices.InvoiceType = BoRcptInvTypes.it_JournalEntry Then ' Si el documento a pagar es un asiento, se debe especificar en que linea del asiento se encuentra el saldo
                    lo_payment.Invoices.DocLine = lo_planillaDet.DocLine
                End If

                ' Se asigna el importe aplicado
                If lo_planillaDet.MonedaDoc = entComun.str_obtMonLocal Then
                    lo_payment.Invoices.SumApplied = lo_planillaDet.Imp_Aplicado
                Else
                    lo_payment.Invoices.AppliedFC = lo_planillaDet.Imp_AplicadoME
                End If

                ' Se añade la linea
                lo_payment.Invoices.Add()

            Next

            ' Se realiza la inserción del objeto en la base de datos
            li_resultado = lo_payment.Add

            ' Se verifica el resultado
            If li_resultado <> 0 Then

                ' Se obtiene y se muestra un mensaje de error
                sub_errorProcesoSAP(po_SBOCompany)

                ' Se muestra un mensaje con los detalles
                sub_mostrarMensaje("Numero de asignacion: " & li_LineaNumAsg & " - Numero de Operacion: " & ls_nroOperacion, System.Reflection.Assembly.GetExecutingAssembly.GetName.Name, Me.GetType.Name.ToString, System.Reflection.MethodInfo.GetCurrentMethod.Name, enm_tipoMsj.error_sis)

            Else ' Si el proceso de adicion a SAP se ejecutó de manera correcta

                ' Se actualiza los DocEntry de los pagos recien ingresados
                po_planilla.PagosR.id = po_planilla.id
                po_planilla.PagosR.idEC = li_idEC
                po_planilla.PagosR.lineaNumAsg = li_LineaNumAsg
                po_planilla.PagosR.DocEntrySAP = po_SBOCompany.GetNewObjectKey

                ' Se añade el detalle
                po_planilla.PagosR.sub_anadir()

            End If

            ' Se retorna el resultado
            Return li_resultado

        Catch ex As Exception
            sub_mostrarMensaje(ex.Message, System.Reflection.Assembly.GetExecutingAssembly.GetName.Name, Me.GetType.Name.ToString, System.Reflection.MethodInfo.GetCurrentMethod.Name, enm_tipoMsj.error_exc)
            Return -1
        End Try
    End Function

    Private Function int_procesarMuchosAMuchos(ByVal po_lstPlanillaDet As List(Of entPlanilla_Lineas), ByVal po_SBOCompany As SAPbobsCOM.Company, ByVal po_planilla As entPlanilla, ByVal pi_tipoAsg As Integer) As Integer
        Try

            ' Se declara una variable para el resultado de la operacion del objeto de SAP
            Dim li_resultado As Integer = 0

            ' Se verifica el ultimo tipo de asignacion
            If pi_tipoAsg = 1 Then ' La asignacion anterior fue UNO a MUCHOS

                ' Se realiza la inserción UNO a MUCHOS
                li_resultado = int_procesarUnoAMuchos(po_lstPlanillaDet, po_SBOCompany, po_planilla)

                ' Se verifica el resultado 
                If li_resultado <> 0 Then

                    ' Se retorna el codigo de error
                    Return li_resultado

                End If

            End If

            If pi_tipoAsg = 2 Then ' La asignacion anterior fue UNO a MUCHOS

                ' Se realiza la inserción UNO a MUCHOS
                li_resultado = int_procesarMuchosAUno(po_lstPlanillaDet, po_SBOCompany, po_planilla)

                ' Se verifica el resultado 
                If li_resultado <> 0 Then

                    ' Se retorna el codigo de error
                    Return li_resultado

                End If

            End If

            ' Se retorna el codigo de error
            Return li_resultado

        Catch ex As Exception
            sub_mostrarMensaje(ex.Message, System.Reflection.Assembly.GetExecutingAssembly.GetName.Name, Me.GetType.Name.ToString, System.Reflection.MethodInfo.GetCurrentMethod.Name, enm_tipoMsj.error_exc)
            Return -1
        End Try
    End Function

    Private Function dte_obtFechaContabPago(pd_fechaContab As Date) As Date
        Try

            ' Se verifica el estado del periodo, en SAP Business One, de la fecha recibida.
            Dim ls_estPeriodo As String = str_verEstadoPeriodo(pd_fechaContab)

            ' Se verifica si se obtuvo el estado del periodo
            If ls_estPeriodo.Trim = "" Then
                sub_mostrarMensaje("No se pudo obtener el estado del periodo para la fecha " & pd_fechaContab.ToString("yyyyMMdd"), System.Reflection.Assembly.GetExecutingAssembly.GetName.Name, Me.GetType.Name.ToString, System.Reflection.MethodInfo.GetCurrentMethod.Name, enm_tipoMsj.error_sis)
                Return pd_fechaContab
            End If

            ' Se retorna la fecha de contabilización del Pago Recibido de acuerdo al estado del periodo de la fecha de contabilización recibida
            If ls_estPeriodo.ToLower.Trim = "y" Then
                Return Now.Date
            Else
                Return pd_fechaContab
            End If

        Catch ex As Exception
            sub_mostrarMensaje(ex.Message, System.Reflection.Assembly.GetExecutingAssembly.GetName.Name, Me.GetType.Name.ToString, System.Reflection.MethodInfo.GetCurrentMethod.Name, enm_tipoMsj.error_exc)
            Return pd_fechaContab
        End Try
    End Function

    Public Sub sub_cancelarPlanilla()
        Try

            ' Se verifica el modo del formulario
            If o_form.Modo = enm_modoForm.BUSCAR Then
                MsgBox("Primero, debe realizar la busqueda de una planilla.")
                Exit Sub
            End If

            ' Se verifica el estado de la planilla: solo se puede cancelar una planilla Abierta o una Cerrada
            Dim ls_estado As String = str_obtEstadoObjeto()
            If ls_estado = "O" Or ls_estado = "C" Then

                ' Se realiza la cancelacion de la planilla
                sub_cancelarPlanilla(ls_estado)

            Else

                ' Se muestra un mensaje que indique que solo se puede cancelar una planilla Abierta o Cerrada
                MsgBox("Solo se puede cancelar una planilla Abierta o Cerrada(Procesada).")

            End If

        Catch ex As Exception
            sub_mostrarMensaje(ex.Message, System.Reflection.Assembly.GetExecutingAssembly.GetName.Name, Me.GetType.Name.ToString, System.Reflection.MethodInfo.GetCurrentMethod.Name, enm_tipoMsj.error_exc)
        End Try
    End Sub

    Private Sub sub_cancelarPlanilla(ByVal ps_estado As String)
        Try

            ' Se muestra un mensaje de confirmacion
            Dim li_confirm As Integer = -1

            ' Se declara una variable para el resultado de la operacion
            Dim ls_res As String = ""

            ' Se obtiene la entidad
            Dim lo_planilla As entPlanilla = obj_obtenerEntidad()

            ' Se obtiene el control con el Id del objeto
            Dim lo_txt As TextEdit = ctr_obtenerControl("id", o_form.Controls)

            ' Se verifica si se obtuvo el control
            If lo_txt Is Nothing Then
                Exit Sub
            End If

            ' Se obtiene el id desde el control
            Dim li_id As Integer = obj_obtValorControl(lo_txt)

            ' Se obtiene la entidad por codigo
            lo_planilla = lo_planilla.obj_obtPorCodigo(li_id)

            ' Se verifica el estado: Si la planilla esta abierta ("O"), se cambia el estado del objeto a Cancelado
            If ps_estado = "O" Then

                ' Se muestra un mensaje de confirmacion
                li_confirm = MessageBox.Show("Al cancelar una planilla abierta, el estado de la misma cambiará a Cancelado. Luego de ello, no podrá realizar modificaciones. ¿Esta seguro que desea cancelar la planilla?", "caption", MessageBoxButtons.YesNoCancel)

                ' Se verifica el resultado del mensaje de confirmacion
                If Not li_confirm = DialogResult.Yes Then
                    Exit Sub
                End If

                ' Se asigna el nuevo Estado 
                lo_planilla.Estado = "A"

                ' Se actualiza el objeto
                ls_res = lo_planilla.str_actualizar

                ' Se verifica el resultado de la operacion
                If ls_res.Trim = "" Then
                    sub_mostrarMensaje("Se cancelo la planilla de manera correcta", System.Reflection.Assembly.GetExecutingAssembly.GetName.Name, Me.GetType.Name.ToString, System.Reflection.MethodInfo.GetCurrentMethod.Name, enm_tipoMsj.exito)
                    sub_asignarEstadoObjeto("A")
                Else
                    sub_mostrarMensaje("No se pudo actualizar el estado de la planilla.", System.Reflection.Assembly.GetExecutingAssembly.GetName.Name, Me.GetType.Name.ToString, System.Reflection.MethodInfo.GetCurrentMethod.Name, enm_tipoMsj.error_sis)
                End If

            Else

                ' Se valida que no exista documentos a revertir en otra otra planilla abierta
                If bol_valDocsECEnPllAbiertasAlAnadir() = False Then
                    Exit Sub
                End If

                ' Se muestra un mensaje de confirmacion
                li_confirm = MessageBox.Show("Al cancelar una planilla cerrada (procesada), se cancela todos los Pagos Recibidos creados en SAP Business One y el estado de la misma cambia a Abierto. ¿Esta seguro que desea cancelar la planilla?", "caption", MessageBoxButtons.YesNoCancel)

                ' Se verifica el resultado del mensaje de confirmacion
                If Not li_confirm = DialogResult.Yes Then
                    Exit Sub
                End If

                ' Se realiza la cancelacion de cada uno de los pagos del detalle de Pagos Recibidos del objeto
                Sub_cancelarPagosRecibidos(lo_planilla)

            End If

        Catch ex As Exception
            sub_mostrarMensaje(ex.Message, System.Reflection.Assembly.GetExecutingAssembly.GetName.Name, Me.GetType.Name.ToString, System.Reflection.MethodInfo.GetCurrentMethod.Name, enm_tipoMsj.error_exc)
        End Try
    End Sub

    Private Sub Sub_cancelarPagosRecibidos(ByVal po_planilla As entPlanilla)
        Try

            ' Se declara una variable para el resultado
            Dim li_resultado As Integer = 0
            Dim ls_mensaje As String = ""

            ' Se realiza la conexion a SAP Business One
            Dim lo_SBOCompany As SAPbobsCOM.Company = entComun.sbo_conectar(s_SAPUser, s_SAPPass)

            ' Se verifica si se realizo la conexion hacia SAP Business One
            If lo_SBOCompany Is Nothing Then
                sub_mostrarMensaje("No se realizó la conexión a SAP Business One.", System.Reflection.Assembly.GetExecutingAssembly.GetName.Name, Me.GetType.Name.ToString, System.Reflection.MethodInfo.GetCurrentMethod.Name, enm_tipoMsj.error_sis)
                Exit Sub
            End If

            ' Se obtiene el progressBar asociado al proceso
            Dim lo_progressBar As System.Windows.Forms.ProgressBar = ctr_obtenerControl("progresoPlanilla", o_form.Controls)

            ' Se verifica si se obtuvo el progressBar
            If Not lo_progressBar Is Nothing Then
                lo_progressBar.Maximum = po_planilla.PagosR.int_contar
                lo_progressBar.Minimum = 0
            End If

            ' Se inicia la transaccion de SAP Business One
            If bol_iniciarTransSBO(lo_SBOCompany) = False Then
                Exit Sub
            End If

            ' Se recorre los pagos recibidos generados en la planilla
            For Each lo_pagoR As entPlanilla_PagosR In po_planilla.PagosR.lstObjs

                ' Se declara un objeto de Payment de SAP Business One
                Dim lo_payment As Payments

                ' Se inicializa el objeto
                lo_payment = lo_SBOCompany.GetBusinessObject(BoObjectTypes.oIncomingPayments)

                ' Se obtiene el Pago Recibido por codigo
                If lo_payment.GetByKey(lo_pagoR.DocEntrySAP) = False Then

                    ' Ocurrio un error al obtener el Pago Recibido
                    sub_mostrarMensaje("Ocurrio un error al obtener el Pago Recibido. DocEntry " & lo_pagoR.DocEntrySAP.ToString, System.Reflection.Assembly.GetExecutingAssembly.GetName.Name, Me.GetType.Name.ToString, System.Reflection.MethodInfo.GetCurrentMethod.Name, enm_tipoMsj.error_sap)

                    ' Se revierte la transaccion
                    bol_RollBackTransSBO(lo_SBOCompany)

                    ' Se desconecta la compañia 
                    lo_SBOCompany.Disconnect()

                    ' Se resetea el progressBar
                    sub_resetProgressBar(lo_progressBar)

                    ' Se retorna un error
                    Exit Sub

                End If

                ' Se realiza la cancelacion del Pago Recibido
                li_resultado = lo_payment.Cancel

                ' Se verifica el resultado de la operacion
                If li_resultado <> 0 Then

                    ' Se muestra un mensaje de error de SAP
                    sub_errorProcesoSAP(lo_SBOCompany)

                    ' Se revierte la transaccion
                    bol_RollBackTransSBO(lo_SBOCompany)

                    ' Se desconecta la compañia 
                    lo_SBOCompany.Disconnect()

                    ' Se resetea el progressBar
                    sub_resetProgressBar(lo_progressBar)

                    ' Se retorna el codigo de error
                    Exit Sub

                End If

                ' Se incrementa el valor del progressBar
                sub_incrementarProgressBar(lo_progressBar)

            Next

            ' Se confirma la transaccion
            If li_resultado = 0 Then

                ' Se confirma la transaccion
                Dim ls_resPla As String = str_CommitTransSBO(lo_SBOCompany)

                ' Se actualiza el objeto de la planilla
                If ls_resPla.Trim = "" Then

                    ' Se actualiza el objeto de la planilla
                    po_planilla.Estado = "O"
                    ls_resPla = po_planilla.str_actualizar()

                    ' Se muestra un mensaje que indica que el proceso se realizó con exito
                    sub_mostrarMensaje("Se canceló la planilla de manera exitosa. (Número de Pagos Cancelados: " & po_planilla.PagosR.int_contar.ToString & "). " & ls_resPla, System.Reflection.Assembly.GetExecutingAssembly.GetName.Name, Me.GetType.Name.ToString, System.Reflection.MethodInfo.GetCurrentMethod.Name, enm_tipoMsj.exito)
                    sub_asignarEstadoObjeto("O")

                Else
                    sub_mostrarMensaje("Ocurrió un error al intentar cancelar los pagos en SAP: " & ls_resPla & "", System.Reflection.Assembly.GetExecutingAssembly.GetName.Name, Me.GetType.Name.ToString, System.Reflection.MethodInfo.GetCurrentMethod.Name, enm_tipoMsj.error_sap)
                End If

            Else

                ' Se revierte la transaccion
                bol_RollBackTransSBO(lo_SBOCompany)

                ' Se muestra un mensaje que indica que ocurrió un error en el proceso
                sub_mostrarMensaje("Ocurrió un error durante la ejecución del proceso", System.Reflection.Assembly.GetExecutingAssembly.GetName.Name, Me.GetType.Name.ToString, System.Reflection.MethodInfo.GetCurrentMethod.Name, enm_tipoMsj.error_sap)

            End If

            ' Se desconecta la compañia 
            lo_SBOCompany.Disconnect()

            ' Se resetea el progressBar
            sub_resetProgressBar(lo_progressBar)

        Catch ex As Exception
            sub_mostrarMensaje(ex.Message, System.Reflection.Assembly.GetExecutingAssembly.GetName.Name, Me.GetType.Name.ToString, System.Reflection.MethodInfo.GetCurrentMethod.Name, enm_tipoMsj.error_exc)
        End Try
    End Sub

#End Region

#Region "Validaciones"

    Private Function bol_valDocsECEnPllAbiertasAlAnadir() As Boolean
        Try

            ' Se obtiene el dataTable del detalle de la planilla
            Dim lo_pllDet As Control = ctr_obtControl("gmi_plaPagosDetalle")

            ' Se verifica si se obtuvo el control
            If lo_pllDet Is Nothing Then
                Return False
            End If

            ' Se obtiene el dataTable desde la grilla obtenida
            Dim lo_dtb As DataTable = CType(lo_pllDet, uct_gridConBusqueda).DataSource

            ' Se verifica si se obtuvo el dataTable
            If lo_dtb Is Nothing Then
                sub_mostrarMensaje("No se pudo obtener el dataTable del detalle de la planilla", System.Reflection.Assembly.GetExecutingAssembly.GetName.Name, Me.GetType.Name.ToString, System.Reflection.MethodInfo.GetCurrentMethod.Name, enm_tipoMsj.error_sis)
                Return False
            End If

            ' Se verifica el modo del formulario para identiicar si se crea una nueva planilla o si se actualiza una existente
            Dim li_idPll As Integer = -1
            If o_form.Modo <> enm_modoForm.ANADIR Then

                ' Se obtiene el control del id de la planilla
                Dim lo_idPll As Control = ctr_obtControl("id")

                ' Se verifica si se obtuvo el control
                If lo_idPll Is Nothing Then
                    Return False
                End If

                ' Se obtiene el id de la planilla
                li_idPll = obj_obtValorControl(lo_idPll)

                ' Se verifica si se obtuvo el id de la planilla 
                If li_idPll < 1 Then
                    sub_mostrarMensaje("Ocurrio un error al obtener el id de la planilla.", System.Reflection.Assembly.GetExecutingAssembly.GetName.Name, Me.GetType.Name.ToString, System.Reflection.MethodInfo.GetCurrentMethod.Name, enm_tipoMsj.error_sis)
                    Return False
                End If

            End If

            ' Se recorre las filas del dataTable
            For Each lo_row As DataRow In lo_dtb.Rows

                ' Se obtiene el id del documento
                Dim li_idDoc As Integer = lo_row("Id_Doc")

                ' Se obtiene el tipo de documento
                Dim li_tipoDoc As Integer = lo_row("Tipo_Doc")

                ' Se obtiene la linea del asiento en donde se encuentra el saldo del documento
                Dim li_docLine As Integer = lo_row("DocLine")

                ' Se obtiene el id del registro del estado de cuenta
                Dim li_idEC As Integer = lo_row("idEC")

                ' Se obtiene el numero de asignacion actual
                Dim li_nroAsig As Integer = lo_row("lineaNumAsg")

                ' Se verifica si el documento existe en otra planilla abierta
                Dim li_verifPll As Integer = entPlanilla.int_verExitDocEnPllAbierta(li_idDoc, li_tipoDoc, li_docLine, li_idPll)
                If li_verifPll > 0 Then
                    sub_mostrarMensaje("El documento " & li_idDoc.ToString & " de tipo " & li_tipoDoc & " está registrado en la planilla " & li_verifPll.ToString & ", la cual se encuentra abierta. (Nro. Asig: " & li_nroAsig.ToString & ")", System.Reflection.Assembly.GetExecutingAssembly.GetName.Name, Me.GetType.Name.ToString, System.Reflection.MethodInfo.GetCurrentMethod.Name, enm_tipoMsj.error_val)
                    Return False
                End If

                ' Se verifica si el registro del estado de cuenta existe en otra planilla abierta
                li_verifPll = entPlanilla.int_verExitECEnPllAbierta(li_idEC, li_idPll)
                If li_verifPll > 0 Then
                    sub_mostrarMensaje("El depósito " & li_idEC.ToString & " está registrado en la planilla " & li_verifPll.ToString & ", la cual se encuentra abierta. (Nro. Asig: " & li_nroAsig.ToString & ")", System.Reflection.Assembly.GetExecutingAssembly.GetName.Name, Me.GetType.Name.ToString, System.Reflection.MethodInfo.GetCurrentMethod.Name, enm_tipoMsj.error_val)
                    Return False
                End If

            Next

            ' Si la validación fue correcta, se retorna TRUE
            Return True

        Catch ex As Exception
            sub_mostrarMensaje(ex.Message, System.Reflection.Assembly.GetExecutingAssembly.GetName.Name, Me.GetType.Name.ToString, System.Reflection.MethodInfo.GetCurrentMethod.Name, enm_tipoMsj.error_exc)
            Return False
        End Try
    End Function

#End Region

#Region "AsientoTC"

    Private Function int_crearAsientoTC(ByVal po_planillaDet As entPlanilla_Lineas,
                                        ByVal po_SBOCompany As SAPbobsCOM.Company,
                                        ByVal po_planilla As entPlanilla,
                                        ps_docEntryPago As String) As Integer
        Try

            ' Se declara una variable para el resultado de la operacion
            Dim li_resultado As Integer = 0

            ' Se obtiene el objeto de configuracion
            Dim lo_entConf As New entConfig
            lo_entConf = lo_entConf.cfg_obtConfiguracionApp

            ' Se declara un objeto de tipo asiento contable de sap business one
            Dim lo_jrnlEntry As SAPbobsCOM.JournalEntries

            ' Se inicializa el objeto de asiento contable de sap business one
            lo_jrnlEntry = po_SBOCompany.GetBusinessObject(BoObjectTypes.oJournalEntries)

            ' Se asigna las propiedades al objeto de asiento contable
            lo_jrnlEntry.ReferenceDate = po_planillaDet.FechaPago
            lo_jrnlEntry.TransactionCode = "AS"
            lo_jrnlEntry.Reference = po_planilla.id
            lo_jrnlEntry.Reference2 = po_planillaDet.idEC
            lo_jrnlEntry.Reference3 = ps_docEntryPago

            ' Se asigna las propiedades al detalle del asiento
            ' - Se verifica la moneda del depósito
            If po_planillaDet.MonedaPag = str_obtMonLocal() Then


                ' Se verifica si el Saldo a Favor es mayor a cero
                If po_planillaDet.SaldoFavor > 0.0 Then

                End If

                ' Se verifica si el importe aplicado es menor al saldo del documento
                If po_planillaDet.MonedaDoc = str_obtMonLocal() Then


                Else

                    ' Se asigna la moneda al detalle del asiento
                    lo_jrnlEntry.Lines.FCCurrency = po_planillaDet.MonedaPag

                    ' Se verifica si el importe aplicado es menor al saldo del documento
                    If po_planillaDet.MonedaDoc = str_obtMonLocal() Then



                    Else



                    End If

                End If

            End If

        Catch ex As Exception
            sub_mostrarMensaje(ex.Message, System.Reflection.Assembly.GetExecutingAssembly.GetName.Name, Me.GetType.Name.ToString, System.Reflection.MethodInfo.GetCurrentMethod.Name, enm_tipoMsj.error_exc)
            Return -1
        End Try
    End Function

#End Region

#Region "Busqueda"

    Public Sub sub_accionBusqueda()
        Try

            ' Se verifica el modo del formulario
            If o_form.Modo = enm_modoForm.BUSCAR Then
                sub_buscarPlanilla()
            Else
                sub_NuevaBusqueda()
            End If

        Catch ex As Exception
            sub_mostrarMensaje(ex.Message, System.Reflection.Assembly.GetExecutingAssembly.GetName.Name, Me.GetType.Name.ToString, System.Reflection.MethodInfo.GetCurrentMethod.Name, enm_tipoMsj.error_exc)
        End Try
    End Sub

    Public Sub sub_buscarPlanilla()
        Try

            ' Se obtiene la entidad desde el formulario
            sub_accionPrincipal()

            ' Se verifica el modo del formulario
            If o_form.Modo <> enm_modoForm.BUSCAR Then sub_habilitacionControl(ctr_obtenerControl("comentario", o_form.Controls), False)

        Catch ex As Exception
            sub_mostrarMensaje(ex.Message, System.Reflection.Assembly.GetExecutingAssembly.GetName.Name, Me.GetType.Name.ToString, System.Reflection.MethodInfo.GetCurrentMethod.Name, enm_tipoMsj.error_exc)
        End Try
    End Sub

    Public Sub sub_NuevaBusqueda()
        Try

            ' Se asigna el modo del formulario
            sub_asignarModo(enm_modoForm.BUSCAR)

        Catch ex As Exception
            sub_mostrarMensaje(ex.Message, System.Reflection.Assembly.GetExecutingAssembly.GetName.Name, Me.GetType.Name.ToString, System.Reflection.MethodInfo.GetCurrentMethod.Name, enm_tipoMsj.error_exc)
        End Try
    End Sub

#End Region

#Region "Reportes"

    Public Sub sub_expPlaCerrada()
        Try

            ' Se verifica el modo del formulario
            If o_form.Modo = enm_modoForm.BUSCAR Then
                MsgBox("Primero, debe realizar la busqueda de una planilla cerrada.")
                Exit Sub
            End If

            ' Se obtiene el estado del documento
            Dim ls_estado As String = str_obtEstadoObjeto()

            ' Se verifica el estado
            If ls_estado <> "C" Then
                MsgBox("Solo se puede exportar una planilla cerrada.")
                Exit Sub
            End If

            ' Se obtiene el grid del detalle de la planilla
            Dim lo_gc As Control = ctr_obtenerControl("gmi_plaPagosDetalle", o_form.Controls)

            ' Se obtiene el dataSource del grid
            Dim lo_dtb As DataTable = CType(lo_gc, uct_gridConBusqueda).DataSource

            ' Se verifica si se obtuvo el dataTable
            If lo_dtb Is Nothing Then
                MsgBox("No hay datos en la grilla del detalle de la planilla.")
                Exit Sub
            End If

            ' Se verifica si el dataTable tiene registros
            If lo_dtb.Rows.Count < 1 Then
                MsgBox("No hay registros en la grilla del detalle de la planilla.")
                Exit Sub
            End If

            ' Se realiza la exportacion del reporte
            sub_genegarReporte(dtb_crearTabla(lo_dtb))

        Catch ex As Exception
            sub_mostrarMensaje(ex.Message, System.Reflection.Assembly.GetExecutingAssembly.GetName.Name, Me.GetType.Name.ToString, System.Reflection.MethodInfo.GetCurrentMethod.Name, enm_tipoMsj.error_exc)
        End Try
    End Sub

    Private Function dtb_crearTabla(ByVal idtbDatos As DataTable) As DataTable
        Dim dtbExport As New DataTable
        Dim strTipMon As String = ""
        Dim col As DataColumn
        col = New DataColumn("NumeroPlanilla", GetType(System.String)) : dtbExport.Columns.Add(col)
        col = New DataColumn("Cliente", GetType(System.String)) : dtbExport.Columns.Add(col)
        col = New DataColumn("NroDoc", GetType(System.String)) : dtbExport.Columns.Add(col)
        col = New DataColumn("Fecha", GetType(System.String)) : dtbExport.Columns.Add(col)
        col = New DataColumn("ImpOrig", GetType(System.String)) : dtbExport.Columns.Add(col)
        col = New DataColumn("ImpApli", GetType(System.String)) : dtbExport.Columns.Add(col)
        col = New DataColumn("Saldo", GetType(System.String)) : dtbExport.Columns.Add(col)
        col = New DataColumn("TC", GetType(System.String)) : dtbExport.Columns.Add(col)
        col = New DataColumn("Monto", GetType(System.String)) : dtbExport.Columns.Add(col)
        col = New DataColumn("Banco", GetType(System.String)) : dtbExport.Columns.Add(col)
        col = New DataColumn("OP", GetType(System.String)) : dtbExport.Columns.Add(col)
        col = New DataColumn("Fecha2", GetType(System.String)) : dtbExport.Columns.Add(col)
        col = New DataColumn("Comend", GetType(System.String)) : dtbExport.Columns.Add(col)
        col = New DataColumn("nroSAP", GetType(System.String)) : dtbExport.Columns.Add(col)

        Dim dr As DataRow
        For i As Integer = 0 To idtbDatos.Rows.Count - 1
            dr = dtbExport.NewRow()
            dr("NumeroPlanilla") = idtbDatos.Rows(i)("id").ToString
            dr("Cliente") = idtbDatos.Rows(i)("Nombre").ToString
            dr("NroDoc") = idtbDatos.Rows(i)("Referencia").ToString
            dr("Fecha") = idtbDatos.Rows(i)("FechaDoc").ToString.Substring(0, 10)
            If (idtbDatos.Rows(i)("monedaDoc").ToString.Equals("SOL")) Then
                strTipMon = "S/. "
                dr("ImpApli") = strTipMon & dbl_redondearImporte(idtbDatos.Rows(i)("imp_aplicado")).ToString
            Else
                strTipMon = "$ "
                dr("ImpApli") = strTipMon & dbl_redondearImporte(idtbDatos.Rows(i)("imp_aplicadoME")).ToString
            End If
            'dr("ImpOrig") = strTipMon & idtbDatos.Rows(i)("total").ToString
            dr("Saldo") = strTipMon & dbl_redondearImporte(idtbDatos.Rows(i)("saldo")).ToString
            dr("ImpOrig") = strTipMon & dbl_redondearImporte(idtbDatos.Rows(i)("saldo")).ToString

            dr("TC") = dbl_redondearImporte(idtbDatos.Rows(i)("tipo_cambio")).ToString
            If (idtbDatos.Rows(i)("MonedaPag").ToString.Equals("SOL")) Then
                strTipMon = "S/. "
            Else
                strTipMon = "$ "
            End If
            dr("Monto") = strTipMon & dbl_redondearImporte(idtbDatos.Rows(i)("montoop")).ToString
            dr("Banco") = entPlanilla.dtb_ObtenerBanco(CInt(idtbDatos.Rows(i)("idEc").ToString))
            dr("OP") = idtbDatos.Rows(i)("nro_operacion").ToString
            dr("Fecha2") = idtbDatos.Rows(i)("fechapago").ToString.Substring(0, 10)
            dr("Comend") = idtbDatos.Rows(i)("ComentarioPl").ToString
            dr("nroSAP") = idtbDatos.Rows(i)("DocNumSAP").ToString
            dtbExport.Rows.Add(dr)
        Next
        Return dtbExport
    End Function

    Private Sub sub_genegarReporte(ByVal idtbDatos As DataTable)
        Dim strRutaFormato As String = Application.StartupPath & "\FormatosExcelReportes\PlanillaCerrada.xlsx"
        Dim strRutaTemp As String = Path.GetTempPath & "PlanillaCerrada" & DateTime.Now.ToString("yyyyMMddHHmmss") & ".xlsx"
        Dim fNewFile As New FileInfo(strRutaTemp)
        Dim fexistingFile As New FileInfo(strRutaFormato)
        Try
            Using MyExcel As New ExcelPackage(fexistingFile)
                Dim iExcelWS As ExcelWorksheet
                iExcelWS = MyExcel.Workbook.Worksheets("hoja1")
                iExcelWS.Cells("A4").LoadFromDataTable(idtbDatos, False)
                iExcelWS.Cells("A4:A" & idtbDatos.Rows.Count + 3).Style.Border.BorderAround(Style.ExcelBorderStyle.Thin)
                ' iExcelWS.Cells("B4").LoadFromDataTable(idtbDatos, False)
                iExcelWS.Cells("B4:B" & idtbDatos.Rows.Count + 3).Style.Border.BorderAround(Style.ExcelBorderStyle.Thin)
                iExcelWS.Cells("C4:C" & idtbDatos.Rows.Count + 3).Style.Border.BorderAround(Style.ExcelBorderStyle.Thin)
                iExcelWS.Cells("D4:D" & idtbDatos.Rows.Count + 3).Style.Border.BorderAround(Style.ExcelBorderStyle.Thin)
                iExcelWS.Cells("E4:E" & idtbDatos.Rows.Count + 3).Style.Border.BorderAround(Style.ExcelBorderStyle.Thin)
                iExcelWS.Cells("F4:F" & idtbDatos.Rows.Count + 3).Style.Border.BorderAround(Style.ExcelBorderStyle.Thin)
                iExcelWS.Cells("H4:H" & idtbDatos.Rows.Count + 3).Style.Border.BorderAround(Style.ExcelBorderStyle.Thin)
                iExcelWS.Cells("J4:J" & idtbDatos.Rows.Count + 3).Style.Border.BorderAround(Style.ExcelBorderStyle.Thin)
                iExcelWS.Cells("L4:L" & idtbDatos.Rows.Count + 3).Style.Border.BorderAround(Style.ExcelBorderStyle.Thin)
                iExcelWS.Cells("N4:N" & idtbDatos.Rows.Count + 3).Style.Border.BorderAround(Style.ExcelBorderStyle.Thin)
                For i As Integer = 4 To idtbDatos.Rows.Count + 3 Step 2
                    iExcelWS.Cells("A" & i & ":N" & i).Style.Border.BorderAround(Style.ExcelBorderStyle.Thin)
                Next
                iExcelWS.Cells("A" & idtbDatos.Rows.Count + 3 & ":N" & idtbDatos.Rows.Count + 3).Style.Border.BorderAround(Style.ExcelBorderStyle.Thin)
                iExcelWS.Cells("A4:A" & idtbDatos.Rows.Count + 3).AutoFitColumns()
                MyExcel.SaveAs(fNewFile)
            End Using
            System.Diagnostics.Process.Start(strRutaTemp)
        Catch ex As Exception
            sub_mostrarMensaje(ex.Message, System.Reflection.Assembly.GetExecutingAssembly.GetName.Name, Me.GetType.Name.ToString, System.Reflection.MethodInfo.GetCurrentMethod.Name, enm_tipoMsj.error_exc)
        End Try
    End Sub

#End Region

End Class
