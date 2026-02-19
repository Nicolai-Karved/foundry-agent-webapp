# CU Paragraph Index Mapping

This document captures the field mappings used to index Content Understanding output into Azure AI Search.

## Source JSON Paths

- Paragraph text: `result.contents[].markdown` (split on double newlines)
- Fields: `result.contents[].fields.<FieldName>.valueString` or `valueDate`
- Page references: `result.contents[].pages[].spans[]` (use `offset` + `length` to map to `pageNumber`)

## Suggested Index Fields

- `id` (key): `${documentId}|${contentIndex}|${paragraphId}`
- `content` (searchable): paragraph text
- `chunkType`: `section` or `paragraph`
- `sectionId` / `sectionTitle`
- `paragraphId`
- `pageNumber`
- `startOffset`
- `length`
- `sourceUrl`
- `blobName`
- `standardId` (from `StandardNumber`)
- `standardTitle` (from `StandardTitle`)

## CU Field Mappings

Each CU field value should be stored in a string field (searchable + filterable):

- `StandardNumber`
- `StandardTitle`
- `PublicationDate`
- `IssuingOrganization`
- `TechnicalCommittee`
- `ApprovalDate`
- `ISBN`
- `ICS`
- `NationalImplementation`
- `CopyrightHolder`
- `CopyrightYear`
- `CommitteeRepresentation`
- `NationalAnnexReference`
- `AmendmentsOrCorrigenda`
- `ProjectInformationRequirements`
- `InformationDeliveryMilestones`
- `InformationStandard`
- `InformationProductionMethodsAndProcedures`
- `ReferenceInformationAndSharedResources`
- `CommonDataEnvironment`
- `InformationProtocol`
- `ExchangeInformationRequirements`
- `AcceptanceCriteria`
- `BIMExecutionPlan`
- `ResponsibilityMatrix`
- `TaskInformationDeliveryPlans`
- `MasterInformationDeliveryPlan`
- `MobilizationPlan`
- `RiskRegister`
- `ProjectCloseOutArchive`
- `LessonsLearned`
- `AppointingParty`
- `LeadAppointedParty`
- `AppointedParty`
- `ProjectTeam`
- `TaskTeam`
- `Project`
- `Originator`
- `VolumeSystem`
- `LevelLocation`
- `Type`
- `Role`
- `Number`
- `Suitability`
- `Revision`
- `Classification`
- `Delimiter`
