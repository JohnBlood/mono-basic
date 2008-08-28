' 
' Visual Basic.Net Compiler
' Copyright (C) 2004 - 2008 Rolf Bjarne Kvinge, RKvinge@novell.com
' 
' This library is free software; you can redistribute it and/or
' modify it under the terms of the GNU Lesser General Public
' License as published by the Free Software Foundation; either
' version 2.1 of the License, or (at your option) any later version.
' 
' This library is distributed in the hope that it will be useful,
' but WITHOUT ANY WARRANTY; without even the implied warranty of
' MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
' Lesser General Public License for more details.
' 
' You should have received a copy of the GNU Lesser General Public
' License along with this library; if not, write to the Free Software
' Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301  USA
' 
#Const DEBUGRESOLVE = True
#If DEBUG Then
#Const EXTENDEDDEBUG = 0
#End If

Imports System.Reflection.Emit

''' <summary>
''' This is the root for the parse tree
''' Start  ::=
'''	[  OptionStatement+  ]
'''	[  ImportsStatement+  ]
'''	[  AttributesStatement+  ]
'''	[  NamespaceMemberDeclaration+  ]
''' </summary>
''' <remarks></remarks>
Public Class AssemblyDeclaration
    Inherits ParsedObject

    ''' <summary>
    ''' The attributes of this assembly.
    ''' </summary>
    ''' <remarks></remarks>
    Private m_Attributes As New Attributes(Me)

    ''' <summary>
    ''' The name of the assembly. 
    ''' </summary>
    ''' <remarks></remarks>
    Private m_Name As String

    ''' <summary>
    ''' All the non-nested types in this assembly.
    ''' </summary>
    ''' <remarks></remarks>
    Private m_Members As New MemberDeclarations(Me)

    ''' <summary>
    ''' All the types as an array of type declarations.
    ''' </summary>
    ''' <remarks></remarks>
    Private m_TypeDeclarations() As TypeDeclaration

    Private m_GroupedClasses As Generic.List(Of MyGroupData)

    Property GroupedClasses() As Generic.List(Of MyGroupData)
        Get
            Return m_GroupedClasses
        End Get
        Set(ByVal value As Generic.List(Of MyGroupData))
            m_GroupedClasses = value
        End Set
    End Property

    ReadOnly Property Members() As MemberDeclarations
        Get
            Return m_Members
        End Get
    End Property

    ReadOnly Property TypeDeclarations() As TypeDeclaration()
        Get
            Return m_TypeDeclarations
        End Get
    End Property

    Sub New(ByVal Parent As Compiler)
        MyBase.New(Parent)
    End Sub

    Sub Init(ByVal Attributes As Attributes)
        If m_Attributes Is Nothing Then
            m_Attributes = Attributes
        Else
            m_Attributes.AddRange(Attributes)
        End If
        m_TypeDeclarations = m_Members.GetSpecificMembers(Of TypeDeclaration).ToArray

        Helper.Assert(m_Members.Count = m_TypeDeclarations.Length)
    End Sub

    Private Function DefineType(ByVal Type As TypeDeclaration) As Boolean
        Dim result As Boolean = True

        result = Type.DefineType AndAlso result

        For Each NestedType As TypeDeclaration In Type.Members.GetSpecificMembers(Of TypeDeclaration)()
            result = DefineType(NestedType) AndAlso result
        Next

        Return result
    End Function

    Private Function DefineTypeHierarchy(ByVal Type As TypeDeclaration) As Boolean
        Dim result As Boolean = True

        result = Type.DefineTypeHierarchy AndAlso result

        For Each NestedType As TypeDeclaration In Type.Members.GetSpecificMembers(Of TypeDeclaration)()
            result = DefineTypeHierarchy(NestedType) AndAlso result
        Next

        Return result
    End Function

    Private Function DefineMembers(ByVal Type As TypeDeclaration) As Boolean
        Dim result As Boolean = True

        Helper.Assert(Type.CecilType IsNot Nothing)

        For Each i As IMember In Type.Members.GetSpecificMembers(Of IMember)()
            If TypeOf i Is TypeDeclaration Then
                'If TypeOf i Is DelegateDeclaration = False Then
                result = DefineMembers(DirectCast(i, TypeDeclaration)) AndAlso result
                vbnc.Helper.Assert(result = (Report.Errors = 0))
                'Else
                'Skip the delagete declarations, they are already defined.
                'End If
            ElseIf TypeOf i Is IDefinableMember Then
                result = DirectCast(i, IDefinableMember).DefineMember AndAlso result
                vbnc.Helper.Assert(result = (Report.Errors = 0))
            Else
                Throw New InternalException("Type " & CObj(i).GetType.ToString & " is not a definable object")
            End If
        Next

        Return result
    End Function

    Friend Function Emit(ByVal Type As TypeDeclaration) As Boolean
        Dim result As Boolean = True

        result = Type.GenerateCode(Nothing) AndAlso result
        result = Type.Members.GenerateCode(Nothing) AndAlso result
        For Each NestedType As TypeDeclaration In Type.Members.GetSpecificMembers(Of TypeDeclaration)()
            result = Emit(NestedType) AndAlso result
        Next

        Return result
    End Function

    Overrides Function ResolveCode(ByVal Info As ResolveInfo) As Boolean
        Dim result As Boolean = True

        For Each type As TypeDeclaration In m_TypeDeclarations
#If EXTENDEDDEBUG Then
            Dim iCount As Integer
            iCount += 1
            Try
                System.Console.ForegroundColor = ConsoleColor.Green
            Catch ex As Exception

            End Try
            Compiler.Report.WriteLine(vbnc.Report.ReportLevels.Debug, "ResolveCode " & type.FullName & " (" & iCount & " of " & m_TypeDeclarations.Length & " types)")
            Try
                System.Console.ResetColor()
            Catch ex As Exception

            End Try
#End If
            result = type.ResolveCode(Info) AndAlso result
            Compiler.VerifyConsistency(result, type.Location)
        Next

        result = m_Attributes.ResolveCode(Info) AndAlso result

        Return result
    End Function

    Function ResolveTypes() As Boolean
        Dim result As Boolean = True

        For Each type As TypeDeclaration In m_TypeDeclarations
#If EXTENDEDDEBUG Then
            Dim iCount As Integer
            iCount += 1
            Compiler.Report.WriteLine(vbnc.Report.ReportLevels.Debug, "ResolveType " & type.FullName & " (" & iCount & " of " & m_TypeDeclarations.Length & " types)")
#End If
            result = ResolveType(type) AndAlso result
        Next

        Return result
    End Function

    Private Shared Function ResolveType(ByVal Type As TypeDeclaration) As Boolean
        Dim result As Boolean = True

        result = Type.ResolveType AndAlso result

        For Each n As TypeDeclaration In Type.Members.GetSpecificMembers(Of TypeDeclaration)()
            result = ResolveType(n) AndAlso result
        Next

        Return result
    End Function

    Function CreateImplicitTypes() As Boolean
        Dim result As Boolean = True

        For Each Type As TypeDeclaration In Me.Types
#If EXTENDEDDEBUG Then
            Dim iCount As Integer
            iCount += 1
            Compiler.Report.WriteLine(vbnc.Report.ReportLevels.Debug, "CreateImplicitTypes " & Type.FullName & " (" & iCount & " of " & m_TypeDeclarations.Length & " types)")
#End If
            Dim tmp As IHasImplicitTypes = TryCast(Type, IHasImplicitTypes)
            If tmp IsNot Nothing Then result = tmp.CreateImplicitTypes AndAlso result

            result = CreateImplicitTypes(Type) AndAlso result
        Next

        Return result
    End Function

    Private Function CreateImplicitTypes(ByVal Type As TypeDeclaration) As Boolean
        Dim result As Boolean = True

        For Each NestedType As TypeDeclaration In Type.Members.GetSpecificMembers(Of TypeDeclaration)()
            result = CreateImplicitTypes(NestedType) AndAlso result
        Next

        For Each Member As IHasImplicitTypes In Type.Members.GetSpecificMembers(Of IHasImplicitTypes)()
            result = Member.CreateImplicitTypes() AndAlso result
        Next

        Return result
    End Function

    Public Overrides Sub Initialize(ByVal Parent As BaseObject)
        MyBase.Initialize(Parent)

        For i As Integer = 0 To m_TypeDeclarations.Length - 1
            m_TypeDeclarations(i).Initialize(Me)
        Next
    End Sub

    Overrides Function ResolveTypeReferences() As Boolean
        Dim result As Boolean = True

        For Each type As TypeDeclaration In m_TypeDeclarations
#If EXTENDEDDEBUG Then
            Dim iCount As Integer
            iCount += 1
            Compiler.Report.WriteLine(vbnc.Report.ReportLevels.Debug, "ResolveTypeReferences " & type.FullName & " (" & iCount & " of " & m_TypeDeclarations.Length & " types)")
#End If
            result = ResolveTypeReferences(type) AndAlso result
        Next

        result = m_Attributes.ResolveTypeReferences AndAlso result

        Return result
    End Function

    Private Overloads Function ResolveTypeReferences(ByVal Type As TypeDeclaration) As Boolean
        Dim result As Boolean = True

        result = Type.ResolveTypeReferences AndAlso result

        If result = False Then Return result

        For Each Member As ParsedObject In Type.Members
            Dim NestedType As TypeDeclaration = TryCast(Member, TypeDeclaration)
            If NestedType IsNot Nothing Then
                result = ResolveTypeReferences(NestedType) AndAlso result
            Else
                result = Member.ResolveTypeReferences() AndAlso result
            End If
            If result = False Then Return result
        Next

        Return result
    End Function

    Function CreateImplicitMembers() As Boolean
        Dim result As Boolean = True

        For Each Type As TypeDeclaration In Me.Types
#If EXTENDEDDEBUG Then
            Dim iCount As Integer
            iCount += 1
            Compiler.Report.WriteLine(vbnc.Report.ReportLevels.Debug, "CreateImplicitMembers " & Type.FullName & " (" & iCount & " of " & m_TypeDeclarations.Length & " types)")
#End If
            Dim tmp As IHasImplicitMembers = TryCast(Type, IHasImplicitMembers)
            If tmp IsNot Nothing Then result = tmp.CreateImplicitMembers AndAlso result

            result = CreateImplicitMembers(Type) AndAlso result
        Next

        Return result
    End Function

    Private Function CreateImplicitMembers(ByVal Type As TypeDeclaration) As Boolean
        Dim result As Boolean = True

        For Each NestedType As TypeDeclaration In Type.Members.GetSpecificMembers(Of TypeDeclaration)()
            result = CreateImplicitMembers(NestedType) AndAlso result
        Next

        For Each Member As IHasImplicitMembers In Type.Members.GetSpecificMembers(Of IHasImplicitMembers)()
            result = Member.CreateImplicitMembers() AndAlso result
        Next

        Return result
    End Function

    Function ResolveMembers() As Boolean
        Dim result As Boolean = True

        For Each type As TypeDeclaration In m_TypeDeclarations
#If EXTENDEDDEBUG Then
            Dim iCount As Integer
            iCount += 1
            Compiler.Report.WriteLine(vbnc.Report.ReportLevels.Debug, "ResolveMembers " & type.FullName & " (" & iCount & " of " & m_TypeDeclarations.Length & " types)")
#End If
            result = ResolveMembers(type) AndAlso result
        Next

        Return result
    End Function

    Private Shared Function ResolveMembers(ByVal Type As TypeDeclaration) As Boolean
        Dim result As Boolean = True

        For Each n As IBaseObject In Type.Members.GetSpecificMembers(Of IBaseObject)()
            Dim nType As TypeDeclaration = TryCast(n, TypeDeclaration)
            Dim nMember As INonTypeMember = TryCast(n, INonTypeMember)

            If nType IsNot Nothing Then
                result = ResolveMembers(nType) AndAlso result
            ElseIf nMember IsNot Nothing Then
                'Resolve all non-type members.
                result = nMember.ResolveMember(ResolveInfo.Default(Type.Compiler)) AndAlso result
            Else
                Helper.Stop() '?
            End If
        Next

        Return result
    End Function


    ''' <summary>
    ''' - Types are defined with the reflection.emit namespace. 
    ''' - Only classes, modules, structures, interfaces, enums, delegates and eventnos (not a type by itself, bu an event might declare a new delegate). They are only defined, nothing else.
    ''' - Classes, modules, structures, interfaces, enums,  delegates and events should implement IDefinable.DefineType()
    ''' </summary>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Function DefineTypes() As Boolean
        Dim result As Boolean = True

        For Each type As TypeDeclaration In m_TypeDeclarations
#If EXTENDEDDEBUG Then
            Dim iCount As Integer
            iCount += 1
            Compiler.Report.WriteLine(vbnc.Report.ReportLevels.Debug, "DefineTypes " & type.FullName & " (" & iCount & " of " & m_TypeDeclarations.Length & " types)")
#End If
            result = DefineType(type) AndAlso result
        Next

        Return result
    End Function

    ''' <summary>
    ''' - Base classes for classes, modules, structures, enums, interfaces and delegates are set.
    ''' - Implemented interfaces for classes are set.
    ''' - Type parameters for classes and structures are set.
    ''' - Classes, modules, structures, interfaces, enums,  delegates and events should implement IDefinable.DefineTypeHierarchy()
    ''' </summary>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Function DefineTypeHierarchy() As Boolean
        Dim result As Boolean = True

        For Each type As TypeDeclaration In m_TypeDeclarations
#If EXTENDEDDEBUG Then
            Dim iCount As Integer
            iCount += 1
            Compiler.Report.WriteLine(vbnc.Report.ReportLevels.Debug, "DefineTypeHierarchy " & type.FullName & " (" & iCount & " of " & m_TypeDeclarations.Length & " types)")
#End If
            result = DefineTypeHierarchy(type) AndAlso result
        Next

        Return result
    End Function

    ''' <summary>
    ''' - All the type's members are defined (methods, constructors, properties, fields, events, operators).
    ''' </summary>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Function DefineMembers() As Boolean
        Dim result As Boolean = True

        For Each type As TypeDeclaration In m_TypeDeclarations
#If EXTENDEDDEBUG Then
            Dim iCount As Integer
            iCount += 1
            Try
                System.Console.ForegroundColor = ConsoleColor.Green
            Catch ex As Exception

            End Try
            Compiler.Report.WriteLine(vbnc.Report.ReportLevels.Debug, "DefineMembers " & type.FullName & " (" & iCount & " of " & m_TypeDeclarations.Length & " types)")
            Try
                System.Console.ResetColor()
            Catch ex As Exception

            End Try
#End If
            result = DefineMembers(type) AndAlso result
        Next

        Return result
    End Function

    Function EmitAttributes() As Boolean
        Dim result As Boolean = True

        If m_Attributes IsNot Nothing Then
            result = m_Attributes.GenerateCode(Nothing) AndAlso result
        End If

        Return result
    End Function

    ''' <summary>
    ''' - All code is emitted for fields with initializers.
    ''' - All the code is emitted for each and every method, constructor, operator and property.
    ''' - Classes, modules, structures, methods, constructors, properties, events, operators should implement IEmittable.Emit(Info as EmitInfo)
    ''' </summary>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Function Emit() As Boolean
        Dim result As Boolean = True

        result = EmitAttributes() AndAlso result

        For Each type As TypeDeclaration In m_TypeDeclarations
#If EXTENDEDDEBUG Then
            Dim iCount As Integer
            iCount += 1
            Try
                System.Console.ForegroundColor = ConsoleColor.Yellow
            Catch ex As Exception

            End Try
            Compiler.Report.WriteLine(vbnc.Report.ReportLevels.Debug, "Emit " & type.FullName & " (" & iCount & " of " & m_TypeDeclarations.Length & " types)")
            Try
                System.Console.ResetColor()
            Catch ex As Exception

            End Try
#End If
            result = Emit(type) AndAlso result
        Next

        SetFileVersion()
        SetAdditionalAttributes()

        Return result
    End Function

    Sub SetAdditionalAttributes()
        'Dim cab As CustomAttributeBuilder

        If Compiler.CommandLine.Define.IsDefined("DEBUG") Then
            Report.WriteLine("Throw New NotImplementedException(): SetAdditionalAttributes")
            'cab = New CustomAttributeBuilder(Compiler.TypeCache.System_Diagnostics_DebuggableAttribute__ctor_DebuggingModes, New Object() {System.Diagnostics.DebuggableAttribute.DebuggingModes.DisableOptimizations Or Diagnostics.DebuggableAttribute.DebuggingModes.Default})
            'Me.Compiler.AssemblyBuilder.SetCustomAttribute(cab)
        End If
    End Sub

    Sub SetFileVersion()
        'Dim product, productversion, company, copyright, trademark As String
        'Dim att() As Object
        'Dim product_att As Reflection.AssemblyProductAttribute = Nothing
        'Dim productversion_att As Reflection.AssemblyVersionAttribute = Nothing
        'Dim company_att As Reflection.AssemblyCompanyAttribute = Nothing
        'Dim copyright_att As Reflection.AssemblyCopyrightAttribute = Nothing
        'Dim trademark_att As Reflection.AssemblyTrademarkAttribute = Nothing

        Report.WriteLine("Throw New NotImplementedException (): SetFileVersion")
        'att = Me.Compiler.AssemblyBuilder.GetCustomAttributes(Compiler.TypeCache.System_Reflection_AssemblyProductAttribute, True)
        'If att.Length > 0 Then product_att = DirectCast(att(0), AssemblyProductAttribute)
        'att = Me.Compiler.AssemblyBuilder.GetCustomAttributes(Compiler.TypeCache.System_Reflection_AssemblyVersionAttribute, True)
        'If att.Length > 0 Then productversion_att = DirectCast(att(0), AssemblyVersionAttribute)
        'att = Me.Compiler.AssemblyBuilder.GetCustomAttributes(Compiler.TypeCache.System_Reflection_AssemblyCompanyAttribute, True)
        'If att.Length > 0 Then company_att = DirectCast(att(0), AssemblyCompanyAttribute)
        'att = Me.Compiler.AssemblyBuilder.GetCustomAttributes(Compiler.TypeCache.System_Reflection_AssemblyCopyrightAttribute, True)
        'If att.Length > 0 Then copyright_att = DirectCast(att(0), AssemblyCopyrightAttribute)
        'att = Me.Compiler.AssemblyBuilder.GetCustomAttributes(Compiler.TypeCache.System_Reflection_AssemblyTrademarkAttribute, True)
        'If att.Length > 0 Then trademark_att = DirectCast(att(0), AssemblyTrademarkAttribute)

        'If product_att IsNot Nothing Then product = product_att.Product Else product = ""
        'If productversion_att IsNot Nothing Then productversion = productversion_att.Version Else productversion = ""
        'If company_att IsNot Nothing Then company = company_att.Company Else company = ""
        'If copyright_att IsNot Nothing Then copyright = copyright_att.Copyright Else copyright = ""
        'If trademark_att IsNot Nothing Then trademark = trademark_att.Trademark Else trademark = ""

        'Me.Compiler.AssemblyBuilder.DefineVersionInfoResource(product, productversion, company, copyright, trademark)
    End Sub

    Public Function SetCecilName(ByVal Name As Mono.Cecil.AssemblyNameDefinition) As Boolean
        Dim result As Boolean = True
        Dim keyfile As String = Nothing
        Dim keyname As String = Nothing
        Dim delaysign As Boolean = False

        Name.Name = IO.Path.GetFileNameWithoutExtension(Compiler.OutFileName)

        If Compiler.CommandLine.KeyFile <> String.Empty Then
            keyfile = Compiler.CommandLine.KeyFile
        End If

        For Each attri As Attribute In Me.Attributes
            Dim attribType As Mono.Cecil.TypeReference
            attribType = attri.ResolvedType

            If Helper.CompareType(attribType, Compiler.TypeCache.System_Reflection_AssemblyVersionAttribute) Then
                result = SetVersion(Name, attri, attri.Location) AndAlso result
            ElseIf Helper.CompareType(attribType, Compiler.TypeCache.System_Reflection_AssemblyKeyFileAttribute) Then
                If keyfile = String.Empty Then keyfile = TryCast(attri.Arguments()(0), String)
            ElseIf Helper.CompareType(attribType, Compiler.TypeCache.System_Reflection_AssemblyKeyNameAttribute) Then
                keyname = TryCast(attri.Arguments()(0), String)
            ElseIf Helper.CompareType(attribType, Compiler.TypeCache.System_Reflection_AssemblyDelaySignAttribute) Then
                delaysign = CBool(attri.Arguments()(0))
            End If
        Next

        If keyfile <> String.Empty Then
            If SignWithKeyFile(Name, keyfile, delaysign) = False Then
                Return result
            End If
        End If

        Return result
    End Function

    '    Public Function GetName() As AssemblyName
    '        Dim result As New AssemblyName()
    '        Dim keyfile As String = Nothing
    '        Dim keyname As String = Nothing
    '        Dim delaysign As Boolean = False

    '        result.Name = IO.Path.GetFileNameWithoutExtension(Compiler.OutFileName)

    '#If DEBUGREFLECTION Then
    '        Helper.DebugReflection_AppendLine(Helper.GetObjectName(result) & " = New System.Reflection.AssemblyName")
    '        Helper.DebugReflection_AppendLine(Helper.GetObjectName(result) & ".Name = """ & result.Name & """")
    '#End If

    '        If Compiler.CommandLine.KeyFile <> String.Empty Then
    '            keyfile = Compiler.CommandLine.KeyFile
    '        End If

    '        For Each attri As Attribute In Me.Attributes
    '            Dim attribType As Mono.Cecil.TypeReference
    '            attribType = attri.ResolvedType

    '            If Helper.CompareType(attribType, Compiler.TypeCache.System_Reflection_AssemblyVersionAttribute) Then
    '                SetVersion(result, attri, attri.Location)
    '            ElseIf Helper.CompareType(attribType, Compiler.TypeCache.System_Reflection_AssemblyKeyFileAttribute) Then
    '                If keyfile = String.Empty Then keyfile = TryCast(attri.Arguments()(0), String)
    '            ElseIf Helper.CompareType(attribType, Compiler.TypeCache.System_Reflection_AssemblyKeyNameAttribute) Then
    '                keyname = TryCast(attri.Arguments()(0), String)
    '            ElseIf Helper.CompareType(attribType, Compiler.TypeCache.System_Reflection_AssemblyDelaySignAttribute) Then
    '                delaysign = CBool(attri.Arguments()(0))
    '            End If
    '        Next

    '        If keyfile <> String.Empty Then
    '            If SignWithKeyFile(result, keyfile, delaysign) = False Then
    '                Return result
    '            End If
    '        End If

    '        Return result
    '    End Function

    Private Function SignWithKeyFile(ByVal result As Mono.Cecil.AssemblyNameDefinition, ByVal KeyFile As String, ByVal DelaySign As Boolean) As Boolean
        Dim filename As String

        filename = IO.Path.GetFullPath(KeyFile)

#If DEBUG Then
        Compiler.Report.WriteLine("Signing with file: " & filename)
#End If

        If IO.File.Exists(filename) = False Then
            Helper.AddError(Me, "Can't find keyfile: " & filename)
            Return False
        End If

        Using stream As New IO.FileStream(filename, IO.FileMode.Open, IO.FileAccess.Read)
            Dim snkeypair() As Byte
            ReDim snkeypair(CInt(stream.Length - 1))
            stream.Read(snkeypair, 0, snkeypair.Length)

            If Helper.IsOnMono Then
                SignWithKeyFileMono(result, filename, DelaySign, snkeypair)
            Else
                If DelaySign Then
                    result.PublicKey = snkeypair
                Else
                    'FIXME: Check if this is correct, I suppose it's not
                    'result.KeyPair = New StrongNameKeyPair(snkeypair)
                    result.PublicKey = snkeypair
                End If
            End If

        End Using

        Return True
    End Function
    
    '    Private Function SignWithKeyFile(ByVal result As AssemblyName, ByVal KeyFile As String, ByVal DelaySign As Boolean) As Boolean
    '        Dim filename As String

    '        filename = IO.Path.GetFullPath(KeyFile)

    '#If DEBUG Then
    '        Compiler.Report.WriteLine("Signing with file: " & filename)
    '#End If

    '        If IO.File.Exists(filename) = False Then
    '            Helper.AddError(Me, "Can't find keyfile: " & filename)
    '            Return False
    '        End If

    '        Using stream As New IO.FileStream(filename, IO.FileMode.Open, IO.FileAccess.Read)
    '            Dim snkeypair() As Byte
    '            ReDim snkeypair(CInt(stream.Length - 1))
    '            stream.Read(snkeypair, 0, snkeypair.Length)

    '            If Helper.IsOnMono Then
    '                SignWithKeyFileMono(result, filename, DelaySign, snkeypair)
    '            Else
    '                If DelaySign Then
    '                    result.SetPublicKey(snkeypair)
    '                Else
    '                    result.KeyPair = New StrongNameKeyPair(snkeypair)
    '                End If
    '            End If

    '        End Using

    '        Return True
    '    End Function

    Private Function SignWithKeyFileMono(ByVal result As Mono.Cecil.AssemblyNameDefinition, ByVal KeyFile As String, ByVal DelaySign As Boolean, ByVal blob As Byte()) As Boolean
        Dim CryptoConvert As Type
        Dim FromCapiKeyBlob As System.Reflection.MethodInfo
        Dim ToCapiPublicKeyBlob As System.Reflection.MethodInfo
        Dim FromCapiPrivateKeyBlob As System.Reflection.MethodInfo
        Dim RSA As Type
        Dim mscorlib As System.Reflection.Assembly = GetType(Integer).Assembly

#If DEBUG Then
        Compiler.Report.WriteLine("Signing on Mono")
#End If

        Try
            RSA = mscorlib.GetType("System.Security.Cryptography.RSA")
            CryptoConvert = mscorlib.GetType("Mono.Security.Cryptography.CryptoConvert")
            FromCapiKeyBlob = CryptoConvert.GetMethod("FromCapiKeyBlob", System.Reflection.BindingFlags.Public Or System.Reflection.BindingFlags.Static Or System.Reflection.BindingFlags.ExactBinding, Nothing, New Type() {GetType(Byte())}, Nothing)
            ToCapiPublicKeyBlob = CryptoConvert.GetMethod("ToCapiPublicKeyBlob", System.Reflection.BindingFlags.Static Or System.Reflection.BindingFlags.Public Or System.Reflection.BindingFlags.ExactBinding, Nothing, New Type() {RSA}, Nothing)
            FromCapiPrivateKeyBlob = CryptoConvert.GetMethod("FromCapiPrivateKeyBlob", System.Reflection.BindingFlags.Static Or System.Reflection.BindingFlags.Public Or System.Reflection.BindingFlags.ExactBinding, Nothing, New Type() {GetType(Byte())}, Nothing)

            If DelaySign Then
                If blob.Length = 16 Then
                    result.PublicKey = blob
#If DEBUG Then
                    Compiler.Report.WriteLine("Delay signed 1")
#End If
                Else
                    Dim publickey() As Byte
                    Dim fromCapiResult As Object
                    Dim publicKeyHeader As Byte() = New Byte() {&H0, &H24, &H0, &H0, &H4, &H80, &H0, &H0, &H94, &H0, &H0, &H0}
                    Dim encodedPublicKey() As Byte

                    fromCapiResult = FromCapiKeyBlob.Invoke(Nothing, New Object() {blob})
                    publickey = CType(ToCapiPublicKeyBlob.Invoke(Nothing, New Object() {fromCapiResult}), Byte())

                    ReDim encodedPublicKey(11 + publickey.Length)
                    Buffer.BlockCopy(publicKeyHeader, 0, encodedPublicKey, 0, 12)
                    Buffer.BlockCopy(publickey, 0, encodedPublicKey, 12, publickey.Length)
                    result.PublicKey = encodedPublicKey
#If DEBUG Then
                    Compiler.Report.WriteLine("Delay signed 2")
#End If
                End If
            Else
                FromCapiPrivateKeyBlob.Invoke(Nothing, New Object() {blob})
                'FIXME: Check if this is correct, I suppose it's not
                'result.KeyPair = New StrongNameKeyPair(blob)
                result.PublicKey = blob
            End If
        Catch ex As Exception
            Helper.AddError(Me, "Invalid key file: " & KeyFile & ", got error: " & ex.Message)
        End Try

    End Function
    
    '    Private Function SignWithKeyFileMono(ByVal result As AssemblyName, ByVal KeyFile As String, ByVal DelaySign As Boolean, ByVal blob As Byte()) As Boolean
    '        Dim CryptoConvert As Type
    '        Dim FromCapiKeyBlob As MethodInfo
    '        Dim ToCapiPublicKeyBlob As MethodInfo
    '        Dim FromCapiPrivateKeyBlob As MethodInfo
    '        Dim RSA As Type
    '        Dim mscorlib As Assembly = GetType(Integer).Assembly

    '#If DEBUG Then
    '        Compiler.Report.WriteLine("Signing on Mono")
    '#End If

    '        Try
    '            RSA = mscorlib.GetType("System.Security.Cryptography.RSA")
    '            CryptoConvert = mscorlib.GetType("Mono.Security.Cryptography.CryptoConvert")
    '            FromCapiKeyBlob = CryptoConvert.GetMethod("FromCapiKeyBlob", BindingFlags.Public Or BindingFlags.Static Or BindingFlags.ExactBinding, Nothing, New Type() {GetType(Byte())}, Nothing)
    '            ToCapiPublicKeyBlob = CryptoConvert.GetMethod("ToCapiPublicKeyBlob", BindingFlags.Static Or BindingFlags.Public Or BindingFlags.ExactBinding, Nothing, New Type() {RSA}, Nothing)
    '            FromCapiPrivateKeyBlob = CryptoConvert.GetMethod("FromCapiPrivateKeyBlob", BindingFlags.Static Or BindingFlags.Public Or BindingFlags.ExactBinding, Nothing, New Type() {GetType(Byte())}, Nothing)

    '            If DelaySign Then
    '                If blob.Length = 16 Then
    '                    result.SetPublicKey(blob)
    '#If DEBUG Then
    '                    Compiler.Report.WriteLine("Delay signed 1")
    '#End If
    '                Else
    '                    Dim publickey() As Byte
    '                    Dim fromCapiResult As Object
    '                    Dim publicKeyHeader As Byte() = New Byte() {&H0, &H24, &H0, &H0, &H4, &H80, &H0, &H0, &H94, &H0, &H0, &H0}
    '                    Dim encodedPublicKey() As Byte

    '                    fromCapiResult = FromCapiKeyBlob.Invoke(Nothing, New Object() {blob})
    '                    publickey = CType(ToCapiPublicKeyBlob.Invoke(Nothing, New Object() {fromCapiResult}), Byte())

    '                    ReDim encodedPublicKey(11 + publickey.Length)
    '                    Buffer.BlockCopy(publicKeyHeader, 0, encodedPublicKey, 0, 12)
    '                    Buffer.BlockCopy(publickey, 0, encodedPublicKey, 12, publickey.Length)
    '                    result.SetPublicKey(encodedPublicKey)
    '#If DEBUG Then
    '                    Compiler.Report.WriteLine("Delay signed 2")
    '#End If
    '                End If
    '            Else
    '                FromCapiPrivateKeyBlob.Invoke(Nothing, New Object() {blob})
    '                result.KeyPair = New StrongNameKeyPair(blob)
    '            End If
    '        Catch ex As Exception
    '            Helper.AddError(Me, "Invalid key file: " & KeyFile & ", got error: " & ex.Message)
    '        End Try

    '    End Function

    Private Function SetVersion(ByVal Name As Mono.Cecil.AssemblyNameDefinition, ByVal Attribute As Attribute, ByVal Location As Span) As Boolean
        Dim result As Version
        Dim version As String = ""

        If Attribute.Arguments IsNot Nothing AndAlso Attribute.Arguments.Length = 1 Then
            version = TryCast(Attribute.Arguments()(0), String)
        Else
            Return ShowInvalidVersionMessage(version, Location)
        End If

        Try
            Dim parts() As String
            Dim major, minor, build, revision As UShort
            parts = version.Split("."c)

            If parts.Length > 4 Then
                Return ShowInvalidVersionMessage(version, Location)
            End If

            If Not UShort.TryParse(parts(0), major) Then
                Return ShowInvalidVersionMessage(version, Location)
            End If

            If Not UShort.TryParse(parts(1), minor) Then
                Return ShowInvalidVersionMessage(version, Location)
            End If

            If parts.Length < 3 Then
                'Use 0
            ElseIf parts(2) = "*" Then
                build = CUShort((Date.Now - New Date(2000, 1, 1)).TotalDays)
                revision = CUShort((Date.Now.Hour * 3600 + Date.Now.Minute * 60 + Date.Now.Second) / 2)
            ElseIf Not UShort.TryParse(parts(2), build) Then
                Return ShowInvalidVersionMessage(version, Location)
            End If

            If parts.Length < 4 Then
                'Use 0
            ElseIf parts.Length > 3 Then
                If parts(3) = "*" Then
                    revision = CUShort((Date.Now.Hour * 3600 + Date.Now.Minute * 60 + Date.Now.Second) / 2)
                ElseIf Not UShort.TryParse(parts(3), revision) Then
                    Return ShowInvalidVersionMessage(version, Location)
                End If
            End If

            result = New Version(major, minor, build, revision)
        Catch ex As Exception
            Return ShowInvalidVersionMessage(version, Location)
        End Try

        Name.Version = result
        Return True
    End Function

    'Private Function SetVersion(ByVal Name As AssemblyName, ByVal Attribute As Attribute, ByVal Location As Span) As Boolean
    '    Dim result As Version
    '    Dim version As String = ""

    '    If Attribute.Arguments IsNot Nothing AndAlso Attribute.Arguments.Length = 1 Then
    '        version = TryCast(Attribute.Arguments()(0), String)
    '    Else
    '        Return ShowInvalidVersionMessage(version, Location)
    '    End If

    '    Try
    '        Dim parts() As String
    '        Dim major, minor, build, revision As UShort
    '        parts = version.Split("."c)

    '        If parts.Length > 4 Then
    '            Return ShowInvalidVersionMessage(version, Location)
    '        End If

    '        If Not UShort.TryParse(parts(0), major) Then
    '            Return ShowInvalidVersionMessage(version, Location)
    '        End If

    '        If Not UShort.TryParse(parts(1), minor) Then
    '            Return ShowInvalidVersionMessage(version, Location)
    '        End If

    '        If parts.Length < 3 Then
    '            'Use 0
    '        ElseIf parts(2) = "*" Then
    '            build = CUShort((Date.Now - New Date(2000, 1, 1)).TotalDays)
    '            revision = CUShort((Date.Now.Hour * 3600 + Date.Now.Minute * 60 + Date.Now.Second) / 2)
    '        ElseIf Not UShort.TryParse(parts(2), build) Then
    '            Return ShowInvalidVersionMessage(version, Location)
    '        End If

    '        If parts.Length < 4 Then
    '            'Use 0
    '        ElseIf parts.Length > 3 Then
    '            If parts(3) = "*" Then
    '                revision = CUShort((Date.Now.Hour * 3600 + Date.Now.Minute * 60 + Date.Now.Second) / 2)
    '            ElseIf Not UShort.TryParse(parts(3), revision) Then
    '                Return ShowInvalidVersionMessage(version, Location)
    '            End If
    '        End If

    '        result = New Version(major, minor, build, revision)
    '    Catch ex As Exception
    '        Return ShowInvalidVersionMessage(version, Location)
    '    End Try

    '    Name.Version = result
    '    Return True
    'End Function

    Private Function ShowInvalidVersionMessage(ByVal Version As String, ByVal Location As Span) As Boolean
        Compiler.Report.ShowMessage(Messages.VBNC30129, Location, "System.Reflection.AssemblyVersionAttribute", Version)
        Return False
    End Function

    ''' <summary>
    ''' Checks whether the specified Type is defined in the current compiling assembly
    ''' </summary>
    ''' <param name="Type"></param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Function IsDefinedHere(ByVal Type As Mono.Cecil.TypeReference) As Boolean
        Helper.Assert(Type IsNot Nothing)
        If TypeOf Type Is Mono.Cecil.ArrayType Then
            Return IsDefinedHere(DirectCast(Type, Mono.Cecil.ArrayType).ElementType)
        ElseIf TypeOf Type Is Mono.Cecil.GenericParameter Then
            Dim gp As Mono.Cecil.GenericParameter = DirectCast(Type, Mono.Cecil.GenericParameter)
            If TypeOf gp.Owner Is Mono.Cecil.TypeDefinition Then
                Return IsDefinedHere(DirectCast(gp.Owner, Mono.Cecil.TypeDefinition))
            ElseIf TypeOf gp.Owner Is Mono.Cecil.MethodDefinition Then
                Return IsDefinedHere(DirectCast(gp.Owner, Mono.Cecil.MethodDefinition))
            Else
                Throw New NotImplementedException
            End If
        ElseIf TypeOf Type Is Mono.Cecil.ReferenceType Then
            Dim tR As Mono.Cecil.ReferenceType = DirectCast(Type, Mono.Cecil.ReferenceType)
            Return IsDefinedHere(tR.ElementType)
        End If
        Return Type.Module.Assembly Is Compiler.AssemblyBuilderCecil
    End Function

    ''' <summary>
    ''' Checks whether the specified Type is defined in the current compiling assembly
    ''' </summary>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Function IsDefinedHere(ByVal Member As Mono.Cecil.MemberReference) As Boolean
        Helper.Assert(Member IsNot Nothing)
        Return Member.DeclaringType.Module.Assembly Is Compiler.AssemblyBuilderCecil
    End Function

    Function FindType(ByVal FullName As String) As TypeDeclaration
        For Each type As TypeDeclaration In Me.Types
            If Helper.CompareName(type.FullName, FullName) Then Return type
        Next
        Return Nothing
    End Function

    Function FindTypeWithName(ByVal Name As String) As TypeDeclaration
        For i As Integer = 0 To m_Members.Count - 1
            Dim tD As TypeDeclaration = TryCast(m_Members(i), TypeDeclaration)
            If td Is Nothing Then Continue For
            If Helper.CompareName(tD.Name, Name) Then Return tD
        Next
        Return Nothing
    End Function

    Property Name() As String
        Get
            Return m_Name
        End Get
        Set(ByVal value As String)
            m_Name = value
        End Set
    End Property

    ReadOnly Property Types() As TypeDeclaration()
        Get
            Return m_TypeDeclarations
        End Get
    End Property

    ReadOnly Property Attributes() As Attributes
        Get
            Return m_Attributes
        End Get
    End Property
End Class