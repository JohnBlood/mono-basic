' 
' Visual Basic.Net Compiler
' Copyright (C) 2004 - 2007 Rolf Bjarne Kvinge, RKvinge@novell.com
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

''' <summary>
''' TypeParameter  ::= 	Identifier  [  TypeParameterConstraints  ]
''' </summary>
''' <remarks></remarks>
Public Class TypeParameter
    Inherits ParsedObject
    Implements INameable

    Private m_Identifier As IdentifierToken
    Private m_TypeParameterConstraints As TypeParameterConstraints
    Private m_GenericParameterPosition As Integer
    Private m_GenericParameterConstraints() As Type
    Private m_Builder As GenericTypeParameterBuilder
    Private m_Descriptor As New TypeParameterDescriptor(Me)

    Sub New(ByVal Parent As ParsedObject)
        MyBase.New(Parent)
    End Sub

    Sub Init(ByVal Identifier As IdentifierToken, ByVal TypeParameterConstraints As TypeParameterConstraints, ByVal GenericParameterPosition As Integer)
        m_Identifier = Identifier
        m_TypeParameterConstraints = TypeParameterConstraints
        m_GenericParameterPosition = GenericParameterPosition
    End Sub

    Function Clone(Optional ByVal NewParent As ParsedObject = Nothing) As TypeParameter
        If NewParent Is Nothing Then NewParent = Me.Parent
        Dim result As New TypeParameter(NewParent)
        result.m_Identifier = m_Identifier
        If m_TypeParameterConstraints IsNot Nothing Then result.m_TypeParameterConstraints = m_TypeParameterConstraints.clone(result)
        Return result
    End Function

    ReadOnly Property TypeDescriptor() As TypeParameterDescriptor
        Get
            Return m_Descriptor
        End Get
    End Property

    ReadOnly Property GenericParameterPosition() As Integer
        Get
            Return m_GenericParameterPosition
        End Get
    End Property

    ReadOnly Property TypeParameterBuilder() As GenericTypeParameterBuilder
        Get
            Return m_Builder
        End Get
    End Property

    ReadOnly Property Identifier() As IdentifierToken
        Get
            Return m_identifier
        End Get
    End Property

    ReadOnly Property TypeParameterConstraints() As TypeParameterConstraints
        Get
            Return m_TypeParameterConstraints
        End Get
    End Property

    Public ReadOnly Property Name() As String Implements INameable.Name
        Get
            Return m_Identifier.Name
        End Get
    End Property

    Public Overrides Function ResolveTypeReferences() As Boolean
        Dim result As Boolean = True

        Me.CheckTypeReferencesNotResolved()
        If m_TypeParameterConstraints IsNot Nothing Then
            result = m_TypeParameterConstraints.ResolveTypeReferences AndAlso result
        End If

        Return result
    End Function

    <Obsolete("No code to resolve here.")> Public Overrides Function ResolveCode(ByVal Info As ResolveInfo) As Boolean
        Dim result As Boolean = True

        'If m_TypeParameterConstraints IsNot Nothing Then result = m_TypeParameterConstraints.ResolveCode AndAlso result

        Return result
    End Function

    Function DefineParameterConstraints(ByVal TypeParameterBuilder As GenericTypeParameterBuilder) As Boolean
        Dim result As Boolean = True

        m_Builder = TypeParameterBuilder

        Dim attributes As GenericParameterAttributes

        attributes = GenericParameterAttributes.None

        If m_TypeParameterConstraints IsNot Nothing Then
            Dim interfaces As New Generic.List(Of Type)
            Dim basetype As Type = Nothing
            For Each constraint As Constraint In m_TypeParameterConstraints.Constraints
                If constraint.TypeName IsNot Nothing Then
                    If constraint.TypeName.ResolvedType.IsInterface Then
                        interfaces.Add(constraint.TypeName.ResolvedType)
                    Else
                        If basetype IsNot Nothing Then
                            Helper.AddError()
                            result = False
                        Else
                            basetype = constraint.TypeName.ResolvedType
                        End If
                    End If
                End If
                attributes = attributes Or constraint.SpecialConstraintAttribute
            Next
            If basetype IsNot Nothing Then m_Builder.SetBaseTypeConstraint(basetype)
            If interfaces.Count > 0 Then m_Builder.SetInterfaceConstraints(interfaces.ToArray)

            If basetype IsNot Nothing Then interfaces.Add(basetype)
            m_GenericParameterConstraints = interfaces.ToArray
        End If

        m_Builder.SetGenericParameterAttributes(attributes)

        Return result
    End Function

    Function GetGenericParameterConstraints() As Type()
        Return m_GenericParameterConstraints
    End Function

    
End Class