# Page 1

Date: 06/06/2023,  
Version Number: P02  Page 0 
 
DOCUMENT UNCONTROLLED WHEN PRINTED 
 
 
 
 
 
 
 
 
 
 
 
 
 
 
 
 
 
 
 
 
 
 
 
 
 
 
 
 
 
 
 
 
 
 
 
 
 
 
 
 
Asset Information 
Requirements (AIR) 
Company-Name 
 
 
 
Author: Author Name 
Title: Technical and Delivery Manager 
 
Telephone: Phone number  
Email: Author@Example.com   
 
Date: 06/06/2023  
Document Ref: 001-SYM-XX-XX-BI-Z-0001-AIR 
Version Number: P02 
Status: S2 – For Information 
 
 
                          Telephone: xxxxxxxxx 
Website: https://xxxx.com/

---

# Page 2

Copyright© Symetri 2025 
Date: 06/06/2023 
Version Number: P02   
Page 1 
DOCUMENT UNCONTROLLED WHEN PRINTED 
Confidentiality, Copyright and Control Procedures 
 
This document is expressly provided to and solely for the use of Company-Name and its contents are confidential. It 
must not be quoted from, referred to, reproduced, loaned to, or distributed to any 3rd party without the prior consent 
of the authoriser. 
 
It is controlled within the Company-Name standard operating procedures systems where the electronic master is 
held and can be accessed on a read only basis, subject to security permissions.  
 
Users of the document are responsible for ensuring that they are working with the current version , for the 
avoidance of doubt please raise with Company-Name if needed. 
 
Paper or electronic copies may in some cases, be taken for remote working etc. However, all paper copies or 
electronic copies not held within the Company-Name systems are uncontrolled. Hence the header 
‘DOCUMENT UNCONTROLLED WHEN PRINTED’ which must not be changed.  
 
Once issued, as a minimum this document shall be reviewed on a yearly basis as deemed necessary by the 
originating team/ function to identify any requirement gaps or changes to industry standards and associated 
workflows. 
 
To enable continuous improvement, all readers are encouraged to notify the author of errors, omissions, and 
any other form of feedback.

---

# Page 3

Copyright© Symetri 2025 
Date: 06/06/2023 
Version Number: P02   
Page 2 
DOCUMENT UNCONTROLLED WHEN PRINTED 
TABLE OF CONTENTS 
 
1 VERSION CONTROL 3 
2 Introduction 4 
2.1 Executive Summary 4 
3 Commercial 5 
3.1 Ownership 5 
3.2 Function (Roles) & Responsibilities 5 
 Project Manager 5 
 Company-Name BIM Specialist / Information Manager 5 
4 Asset Information Requirements 6 
4.1 Asset Definition 6 
4.2 List of Maintainable Assets 6 
4.3 Tagging of Assets 6 
4.4 Construction Operations Building Information Exchange (COBie) 7 
4.5 Classification 8 
5 Information 9 
5.1 Required Fields 9 
5.2 Format of the required fields 9 
5.3 Linked O&M 13 
5.4 IFC File deliverable 13 
5.5 Delivery Strategy for Asset Information 14 
6 As Built Asset Information Model Requirements 15 
6.1 Level of information need 15 
6.2 File Formats 16 
6.3 Verification & Validation 17 
7 Glossary 18 
7.1 Terms & Acronyms 18 
APPENDIX A: ROOM NUMBERING GUIDELINES 18

---

# Page 4

Date: 06/06/2023 
Version Number: P02  
Page 3 
DOCUMENT UNCONTROLLED WHEN PRINTED 
1 VERSION CONTROL 
 
 
Version Authored Date Approved Version Comments 
P01 Steve Rudge 2/05/2023  Draft Issue for approval and comments 
P02 Steve Rudge 06/06/2023  
Amended to suit Company-Name 
comments

---

# Page 5

Date: 06/06/2023 
Version Number: P02  
Page 4 
DOCUMENT UNCONTROLLED WHEN PRINTED 
2 INTRODUCTION 
2.1 Executive Summary 
 
This Asset Information Requirements (AIR) document intends to set -out the detailed asset information 
requirements for Company-Name.  
 
This AIR forms part of the Information Management Process (IMP) and shall be incorporated into the Tender 
documentation for Lead Appointed Party (Architect) and Appointed Parties (Consultants & Sub-Contractors) and 
will be part of the contractual documentation. 
 
This document has been produced in accordance with BS EN ISO 19650 standards  and shall continue to be 
reviewed on an ann ual basis as deemed necessary  with any updates required being informed from lessons 
learned during project implementation. Company-Name partnered with the Symetri UK team in the generation of 
this AIR document and can be approached for any clarity and guidance through the following contact:  
 
Technical and Delivery Manager 
Author Name  
Phone number   
Author@Example.com   
 
This document should be read in conjunction with other project documentation and Appendices, especially with 
respect to: 
 
• Company-Name, Exchange Information Requirements (EIR) (001-SYM-XX-XX-BI-Z-0002-EIR) 
• Asset requirement COBie Matrix (001-SYM-XX-XX-BI-Z-0003-Asset)

---

# Page 6

Date: 06/06/2023 
Version Number: P02  
Page 5 
DOCUMENT UNCONTROLLED WHEN PRINTED 
3 COMMERCIAL 
3.1 Ownership 
 
This AIR document and the information within shall be maintained and kept up to date by Company-Name. 
3.2 Function (Roles) & Responsibilities 
 Technical Manager 
 
The Company-Name Technical Manager shall be responsible for the inclusion of this AIR document into project 
Tender information as part of the E xchange Information Requirements (EIR) for project delivery and to support 
the BIM process by communicating  the Asset Information Requirements to the Appointed Parties for successful 
delivery. 
 
The Lead A ppointed Party (Architect), Appointed BIM Specialist and Appointed Parties (Consultants & Sub -
Contractors) shall be responsible for ensuring delivery of Asset Information in accordance with this AIR document 
and shall put into place governance and compliance procedures to provide assurance to the  Company-Name 
team that information is being produced in accordance with the requirements set.  
 
Prior to handover of the project information, all Appointed Parties (Consultants & Sub -Contractors) shall be 
responsible for verifying and validating the project information supplied meets the specified requirements of this 
AIR document. Proving, with supporting evidence, the requirements have been met and the information is suitable 
to be transferred into the Asset Information Model (AIM) Systems.   
 
 Company-Name/ Appointed Parties 
 
Company-Name (Appointing Party) shall be responsible for the maintenance and up -keep of this AIR document 
and management of the Asset Information Model.  
 
The Technical Manager shall be responsible for receiving the information at the end of the project at the point of 
Handover and Close Out , accepting the information once verified by the Lead Appointed Party (Architect), 
Appointed BIM Specialist and Appointed Parties (Consultants & Sub-Contractors).

---

# Page 7

Date: 06/06/2023 
Version Number: P02  
Page 6 
DOCUMENT UNCONTROLLED WHEN PRINTED 
4 ASSET INFORMATION REQUIREMENTS 
4.1 Asset Definition 
The definition of an Asset for Company-Name is ‘Something that has a requirement for regular Managed 
Maintenance and compliance management’.  This also includes items that need to be managed by law, or 
compliant with statutory regulations.  
 
Company-Name would like to ensure that data captured can be flexible and linked to various CAFM (Computer 
Aided Facility Management) systems that will allow COBie information to be extracted directly from a standard 
UK 2012 format COBie data drop and link to the system. The sheets that will be required are: 
• Contact 
• Facility 
• Floor 
• Space 
• Type 
• Component 
• System 
 
All other COBie sheets are not required on this project. 
 
This will include the Space/  Room information from the COBie.space worksheet and the mainta inable asset 
information that will be brought through from the COBie.type and COBie.component worksheets. 
4.2 List of Maintainable Assets 
Company-Name will maintain a CAFM system and require structured data from its supply chain to  allow for the 
efficient transfer of data from Projects to the Facility Management system. To provide a comprehensive list of 
required Maintainable Assets , a base around industry practice and aligned to the Uniclass 2015 classification 
system has been provided in 001-SYM-XX-XX-BI-Z-0003-Asset document. 
  
Where Assets are not listed in 001 -SYM-XX-XX-BI-Z-0003-Asset but do require plan preventative maintenance 
(PPM) information and form part of the design, the Lead Appointed Party (Architect) must inform Company-Name 
who will record the new equipment type, and ensure a placeholder is available within the Company-Name’s CAFM 
system ready for asset data migration. The Lead Appointed Party (Architect) along with the Appointed BIM 
Specialist and Appointed Parties (Consultants & Sub-Contractors) must define in the BEP who is responsible for 
delivering the information requirements for each asset type. 
4.3 Tagging of Assets 
Company-Name require all maintainable assets to be given a unique identity and  should be agreed with the 
Company-Name so the project team can include in the structured data drop. This shall be fully detailed in the 
project BEP. There is no requirement to fully tag all maintainable assets out on site.

---

# Page 8

Date: 06/06/2023 
Version Number: P02  
Page 7 
DOCUMENT UNCONTROLLED WHEN PRINTED 
4.4 Construction Operations Building Information Exchange (COBie)  
In order to deliver and collect the required Asset Data for Facilities Management purposes, Company-Name 
specify that non-geometrical data is submitted at the point of Handover and Close Out in a COBie structured data 
drop aligned to the UK 2012 template . This requirement data should be aligned to that defined in the  data at 
agreed project stages as stated within the projects E xchange Information Requirements (EIR) document  and 
should be documented in the Project BEP. 
 
1. Construction Operations Building information exchange (COBie) that conforms the data structure described 
in BS EN ISO 19650 -4:2022 is required as an information exchange format to inform procurement, 
construction, operation & management of the asset. COBie shall be progressively developed throughout the 
lifecycle of the project by the Delivery Team, through both design and construction.  
 
2. The Appointed BIM Specialist along with the  Lead Appointed Party (Architect) and Appointed Parties 
(Consultants & Sub -Contractors) shall detail the methods and procedures within the BIM Execution Plan 
confirming how COBie shall be progressively developed and delivered to Company-Name.  
 
3. The required scope of COBie can be found within section 4 of this AIR and within the COBie Matrix 001-SYM-
XX-XX-BI-Z-0003-Asset. 
 
a) The COBie Matrix 
i. The COBie Matrix provides clear scope definition to the COBie data structure required by 
Company-Name, the delivery milestone the require data field is to be included in the data 
drop. It will be the responsibility of the Lead Appointed Party (Architect) and Appointed BIM 
Specialist to develop the COBie Matrix to including the responsible Task Team for each data 
field.  
 
b) Additional Attribute Data  
i. Additional attributes are not required by Company-Name. 
 
c) Maintainable Asset List 
i. Key Maintainable assets that shall form the COBie Information Exchange are listed, 
referencing their name and classification.  
ii. The Maintainable Asset List has been developed to support the Invitation to Tender (ITT) 
and Tender response process. It is recognised that this list will vary between projects. To 
refine the Maintainable Asset List for the nature of this project, all Appointed Parties shall 
finalise and agree all maintainable assets with the Company-Name prior to appointment.  
 
d) COBie to CAFM mapping  
i. The Appointed Parties are required to work with the Company-Name to ensure the COBie 
data will map into the CAFM system.  
ii. COBie data to BS EN ISO 19650 -4:2022 shall form the basis of data for the CAFM 
requirements.

---

# Page 9

Date: 06/06/2023 
Version Number: P02  
Page 8 
DOCUMENT UNCONTROLLED WHEN PRINTED 
4. It is of significant importance that the COBie information exchange conforms to that described within BS EN 
ISO 19650-4:2022. 
4.5 Classification 
 
The Appointed BIM Specialist along with the Lead Appointed Party (Architect) and Appointed Parties (Consultants 
& Sub-Contractors) shall fully document procedures to capture and shall confirm the details for the Classification 
of model information within the BIM Execution Plan (BEP). Classification schemas to be adopted are detailed 
below: 
 
Note – Classification is only needed against maintainable assets only.

---

# Page 10

Date: 06/06/2023 
Version Number: P02  
Page 9 
DOCUMENT UNCONTROLLED WHEN PRINTED 
5 INFORMATION 
5.1 Required Fields 
Each maintainable asset will require the fields identified in the spreadsheet in 001-SYM-XX-XX-BI-Z-0003-Asset. 
These fields can be added to the model via a shared/project shared parameter file or custom attributes depending 
on the modelling software.  The fields that are required will cover the physical maintainable assets and 
rooms/spaces in the building. 
 
5.2 Format of the required fields 
The following tables gives details and examples of the format that should be presented in the COBie data drop.  
 
COBie Contact Name Description Example Required? 
 Email  Email address of main contact Example@Architect.com 
 
 CreatedBy Whom the data drop was created 
by 
User@Architect.com 
 
 CreatedOn The date the data drop was 
created 
2022-XX-XXT12:00:00 
 
 Category Role on project Architect 
 
 Company The name of the Company Example Architects Ltd 
 
 Phone The Company Phone number +44 7543 222861 
 
 ExtSystem ** Not Required ** Not Required 
 
 ExtObject ** Not Required ** Not Required 
 
 ExtIdentifier  ** Not Required ** Not Required 
 
 Department The studio to which the project 
relates 
London Region 
 
 Organization Code ** Not Required ** Not Required 
 
 GivenName Contact first name 
 
User 
 
 FamilyName Contact Surname Example 
 
 Street Office Address Office Address 
 
 PostalBox Postal Box PO Box 111 
 
 Town Company Town location within 
country 
Town Location 
 
 StateRegion Company Region Location within 
country 
Region Location 
 
 PostalCode Post Code of Company office AA11 1AA 
 
 Country Company Country Location Country Location

---

# Page 11

Date: 06/06/2023 
Version Number: P02  
Page 10 
DOCUMENT UNCONTROLLED WHEN PRINTED 
 
COBie Facility Name Description Example Required? 
 Name Name of Project Project X 
 
 CreatedBy Name of person creating Data 
Drop 
User@example.com  
 
 CreatedOn Date and Time when Data Drop 
was created 
2022-07-05T07:36:19 
 
 Category Category number for Project Co_35_50_58 
  
 Project Name Name of Project Project X 
 
 SiteName Name of Site Project X ** Not 
Required– 
Can be left 
populated 
 LinearUnits Type of Linear Units used Millimetres 
 
 AreaUnits Type of Area Units used Square Meters 
 
 VolumeUnits Type of Volume Units used Cubic Meters 
 
 CurrencyUnits Type of Currency used Pounds 
 
 AreaMeasurment Area Measurement used RICS / BCIS 
 
 ExternalSystem ** Not Required ** Not Required 
 
 ExternalProjectObject ** Not Required ** Not Required 
 
 ExternalProjectIdentifier ** Not Required ** Not Required 
 
 ExternalSiteObject ** Not Required ** Not Required 
 
 ExternalSiteIdentifier ** Not Required ** Not Required 
 
 ExternalFacilityObject ** Not Required ** Not Required 
 
 ExternalFacilityIdentifier ** Not Required ** Not Required 
 
 Description A Description of the Type of 
building 
Development of a new 
expanded 70 bed nursing 
home 
 
 ProjectDescription A Description of the Type of 
Project 
Nursing Home 
 
 SiteDescription The Site Description needs to 
include the site name, Road 
Address, Town Location, 
County Location and the 
Postcode 
xxxxxx 
  
 Phase Should align to the RIBA plan of 
works 
RIBA Stage 6 - Handover 
 
     
 
 
 
COBie Floor Name Description Example Required? 
 Name The Names of the Floors within the 
project 
Level 01 
 
 CreatedBy The Name of the person creating 
the Data Drop 
User@Example.com  
 
 CreatedOn ** Not Required ** Not Required 
 
 Category ** Not Required ** Not Required 
 
 ExtSystem ** Not Required– Can be left 
populated 
** Not Required– Can be 
left populated  
 ExtObject ** Not Required– Can be left 
populated 
** Not Required– Can be 
left populated  
 ExtIdentifier ** Not Required– Can be left 
populated 
** Not Required– Can be 
left populated  
 Description ** Not Required ** Not Required 
 
 Elevation Elevation in relation to Sea Level (Ground floor example) 
52750  
 Height Height in relation to distant from 
Ground Floor level 
(First floor example) 4000

---

# Page 12

Date: 06/06/2023 
Version Number: P02  
Page 11 
DOCUMENT UNCONTROLLED WHEN PRINTED 
 
 
 
COBie Space Name Description Example Required? 
 Name The Name of the Space should be 
unique to the project (Room 
Number) 
A10 
 
 CreatedBy Name of the User creating the data 
drop 
User@Example.com  
 
 CreatedOn ** Not Required ** Not Required 
 
 Category The Category should be the Space 
name and the Descriptions - 
Uniclass 
SL_20_15_59:Office 
  
 FloorName The Name of the Floor Level 01 
 
 Description The Name of the Space Office 1 
 
 ExtSystem ** Not Required ** Not Required 
 
 ExtObject ** Not Required ** Not Required 
 
 ExtIdentifier ** Not Required ** Not Required 
 
 RoomTag Sign fixed to door if different to 
Name 
01-A10 
 
 UsableHeight ** Not Required ** Not Required 
  
 GrossArea ** Not Required ** Not Required 
 
 NetArea ** Not Required ** Not Required 
 
 
COBie Type Name Description Example Required? 
 Name The Company-Name_The 
Type Name (Found on 
design drawing 
schedules) with NO Revit 
ID 
SYM_DT-1  
 
 
(Example for Door Type 1) 
 
 
 CreatedBy This will contain the email 
of the person who created 
the Data drop for this 
element 
User@Example.com  
 
 CreatedOn ** Not Required ** Not Required 
 
 Category This will contain the 
elements UniClass code + 
The description 
Pr_30_59_23 
Door Frame and Leaves  
 Description The Description can 
include a specification 
reference 
Specification reference 
 
 AssetType ** Not Required ** Not Required 
 
 Manufacturer The Email address of the 
manufacturer must 
populate this field 
Company@email.com 
 
 ModelNumber This needs to include the 
reference to the element 
product number along 
with a specific size 
TX/800x300x1500 
 
 WarrantyGuarantorParts The Email address of the 
manufacturer must 
populate this field 
Company@email.com 
 
 WarrantyDurationParts This field must include the 
warranty duration 
5 
 
 WarrantyGuarantorLabour ** Not Required ** Not Required 
 
 WarrantyDurationLabour ** Not Required ** Not Required 
 
 WarrantyDurationUnit The warranty duration unit Years 
 
 ExtSystem ** Not Required– Can be 
left populated 
** Not Required– Can be left 
populated  
 ExtObject ** Not Required– Can be 
left populated 
** Not Required– Can be left 
populated  
 ExtIdentifier ** Not Required– Can be 
left populated 
** Not Required– Can be left 
populated

---

# Page 13

Date: 06/06/2023 
Version Number: P02  
Page 12 
DOCUMENT UNCONTROLLED WHEN PRINTED 
 ReplacementCost ** Not Required ** Not Required 
 
 ExpectedLife ** Not Required ** Not Required 
 
 DurationUnit ** Not Required ** Not Required 
 
 WarrantyDescription ** Not Required ** Not Required 
 
 NominalLength The Length of the element 1200 
 
 NominalWidth The Width of the element 50 
 
 NominalHeight The Height of the element 1400 
 
 ModelReference ** Not Required ** Not Required 
  
 Shape ** Not Required ** Not Required 
 
 Size ** Not Required ** Not Required 
 
 Colour ** Not Required ** Not Required 
 
 Finish ** Not Required ** Not Required 
 
 Grade ** Not Required ** Not Required 
 
 Material ** Not Required ** Not Required 
 
 Constituents Constituents – Iron 
Mongery reference 
For Doors and Windows 
 
 Features Features included with 
product 
Auto-close 
 
 AccessibilityPerformance ** Not Required ** Not Required 
 
 CodePerformance ** Not Required ** Not Required 
 
 SustainabilityPerformance ** Not Required ** Not Required 
 
 
COBie 
Component 
Name Description Example Required? 
 Name The Type Name_Unique 
Reference_Revit ID 
SYM_DT-1_SYM_E_A001_610943 
 
 
 
 CreatedBy This will contain the email of 
the person who created the 
Data drop for this element 
User@Example.co.uk 
 
 CreatedOn ** Not Required ** Not Required 
 
 TypeName The Company-Name_The 
Type Name (Found on 
design drawing schedules) 
with NO Revit ID 
SYM_DT-1  
 
(Example for Door Type 1) 
 
 SpaceNames The Name of the Space 
should be the Floor followed 
by the unique room number 
(WP NEEDS to be removed) 
A10 
 
 Description This should include a good 
description of the asset. 
Single 30 min fire door Type 1 
 
 ExtSystem ** Not Required– Can be left 
populated 
** Not Required– Can be left 
populated  
 ExtObject ** Not Required– Can be left 
populated 
** Not Required– Can be left 
populated  
 ExtIdentifier ** Not Required– Can be left 
populated 
** Not Required– Can be left 
populated  
 SerialNumber This should include the 
serial number (if suitable) 
S4567901 
 
 InstallationDate ** Not Required ** Not Required 
 
 WarrantyStartDate (=PC 
date) 
This should include the start 
date of this Component 
2021‑07-10 
 
 TagNumber  Unique Reference for the 
project 
ID0002 (to be defined in the BEP) 
 
 Barcode ** Not Required ** Not Required 
 
 AssetIdentifier SFG 20 code and 
description 
23-17 Fire Doors

---

# Page 14

Date: 06/06/2023 
Version Number: P02  
Page 13 
DOCUMENT UNCONTROLLED WHEN PRINTED 
 
COBie System Name Description Example Required? 
 Name System Description name CCTV System 
 
 
 
 CreatedBy This will contain the email of the 
person who created the Data drop 
for this element 
User@Example.co.uk 
 
 CreatedOn ** Not Required ** Not Required 
 
 Category Uniclass Ss Reference for the 
system 
Ss_75_40_53_86 
Surveillance System  
 ComponentNames ** Not Required ** Not Required 
 
 ExtSystem ** Not Required ** Not Required 
 
 ExtObject ** Not Required ** Not Required 
 
 Extldentifire ** Not Required ** Not Required 
 
 Description System Description to identify 
main manufacturer inc phone 
number 
Secom CCTV System Tel 
077343 28302  
 
5.3 Linked O&M 
The O&M Manuals shall be provided by the Appointed Parties on this project as defined by the Scope of Works 
documents. The file shall contain sufficient information for Company-Name to safely operate and maintain this 
facility. These is not a requirement to provide any links or URL links to the O&M data. 
 
Company-Name require an electronic copy of the O&M Manual in bookmarked .PDF format. Each file shall not 
exceed 50Mb in size. If the file does exceed this size, the file shall be split into volumes (e.g. volume 1, volume 2 
and so on). With bookmarks to identify the context. All information within each file shall be indexed and searchable 
for the ease and accessibility of use. 
 
An electronic copy of all warranties shall be provided to the client in .PDF format and shall be included within the 
relevant section of the O&M manuals. If this is not provided then the O&M manuals will be deemed to be 
incomplete.  
5.4 IFC File deliverable 
Company-Name do not require a structured data come set through the strucutred IFC output. For this reason a 
IFC 2x3 Basic FM file format shall be provided at the end of the project to future protect just to capture the 
geometry only.

---

# Page 15

Date: 06/06/2023 
Version Number: P02  
Page 14 
DOCUMENT UNCONTROLLED WHEN PRINTED 
5.5 Delivery Strategy for Asset Information 
 
The Lead Appointed Party (Architect) and Appointed Parties (Consultants & Sub-Contractors) shall be responsible 
for the production of the Asset Information Model (AIM). The AIM shall consist of models, drawings, O&M 
documentation and COBie data representing the as -constructed/ as-installed asset. Information to be included 
within the AIM shall consist of:  
a) 3D models in their native (unfederated) format – Open file formats (IFC) shall be supplied also.  
b) PDF and DWG  drawings cut directly from the models  
c) A Federated 3D model  
d) Documents in PDF - O&M manuals, surveys reports etc  
e) Non-geometrical information. COBie data that conforms to the standards set out within BS EN ISO 19650-
4:2022.  
 
The methods and procedures to ensure quality and integrity of AIM shall be documented in the BIM Execution 
Plan.  
 
The AIM shall be developed throughout construction phases by the Appointed BIM Specialist alongside the Lead 
Appointed Party (Architect) and exchanged through the project Common Data Environment (CDE).  
 
The AIM shall be handed over to Company-Name and shall be audited by the Appointing Party (Company-Name) 
BIM via the Appointed BIM Specialist . Only information that conforms to the requirements of this EIR shall be 
accepted. Non-compliant information shall be rejected, if rejected the Appointed Parties have  5 working days to 
resolve.  
 
The Appointed Parties are required to provide as-built/ as-installed information to Company-Name to support 
the delivery of asset information as per the Scope of Works. In addition, the as-built/ as-installed information 
shall be updated at the end of the 12-month aftercare period to capture resolution of defects that may occur 
within the period as deemed necessary by Company-Name.

---

# Page 16

Date: 06/06/2023 
Version Number: P02  
Page 15 
DOCUMENT UNCONTROLLED WHEN PRINTED 
6 AS BUILT ASSET INFORMATION MODEL REQUIREMENTS 
6.1 Level of information need 
 
The Information needs required for the Asset Information Model shall be in accordance with the below 
statements. 
 
Stage Number 
Model Name 
6  
Handover & Close Out 
Systems to be 
Covered 
All 
Geometrical 
Illustration 
The Level of Model definition, LOD and LOI requirements shall be aligned to 
that defined in the EIR. 
What can the 
Model be relied 
upon for 
An accurate record of the asset as constructed at handover, 
including all information required for operation and maintenance as defined in this AIR. 
Output 
Individual discipline as-constructed model in native and IFC formats. 
2D PDF & DWG  drawings derived from the model where possible. 
Federated, clash resolved model. 
Structured COBie data drop of the maintainable asset information. 
Parametric 
Information 
Updated geometry and installed product information, “as constructed” accuracy / resolution of 
information.  
 
Critical 
Interfaces & 
Logic 
As constructed photographic records 
Element performance test results 
System commissioning status 
Construction 
Requirements 
Confirmed status that the construction aids have been removed

---

# Page 17

Date: 06/06/2023 
Version Number: P02  
Page 16 
DOCUMENT UNCONTROLLED WHEN PRINTED 
6.2 File Formats 
 
Information Type Description / Scope Deliverable Formats 
Documentation 
This includes all views / sheet 
files (Drawings) produced from 
the BIM Models.  
Individual Drawings produced 
from any BIM/CAD files and 
documentation such as reports in 
Microsoft Office formats.  
PDF Export 
Native document formats (EG: DWG) 
Geometrical Information 
This includes all model files 
produced from BIM/CAD systems.  
IFC Export – IFC 2X3 Basic FM 
Handover View. 
Native CAD/BIM formats. 
NWC for collaboration (not for IFC files). 
NWD of the federated model (if 
Navisworks is being utilised) 
Non-Geometrical Data 
This is to include all room and 
maintainable assets as defined in 
previous sections of this AIR 
MS Excel .XLSX COBie file 
Broken down as required, this does not 
need to be federated into a single data 
drop.

---

# Page 18

Date: 06/06/2023 
Version Number: P02  
Page 17 
DOCUMENT UNCONTROLLED WHEN PRINTED 
6.3 Verification & Validation 
 
The Lead Appointed Party (Architect), Appointed BIM Specialist and Appointed Parties (Consultants & Sub-
Contractors) are required to  verify to Company-Name that the information  has been populated to suit the 
requirements of this AIR and it is the responsibility of the party who are installing or providing the assets to ensure 
the data is accurate prior to acceptance of Handover & Close Out and that it is a true representation of what has 
been constructed/ installed.  
 
Areas with complex systems, plant, equipment or items that are hidden due to boxing in shall be verified as being 
accurate and complete with photographic evidence and camera location references as a minimum as appropriate. 
This can be presented on a marked-up floor plan and a structured folder system containing the photos in the CDE. 
The process should be defined in the project BIM Execution Plan (BEP). 
 
For non-complex systems/ areas traditional surveying techniques can be used to verify the location of assets in 
the model. Company-Name via the Appointed Parties are to audit the model against the as-constructed state of 
the assets before sign off. 
 
The Technical Manager shall accept the Information for Handover & Close Out once satisfied that the Information 
provided meets the requirements specification.

---

# Page 19

Date: 06/06/2023 
Version Number: P02  
Page 18 
DOCUMENT UNCONTROLLED WHEN PRINTED 
7 GLOSSARY 
7.1 Terms & Acronyms 
 
Refer to the EIR 001-SYM-XX-XX-BI-Z-0002-EIR document for a full list of terms and Acronyms. 
 
APPENDIX A: ROOM NUMBERING GUIDELINES 
 
The following guidelines have been developed to explain the procedure for establishing room numbers for new 
buildings and areas where alterations have been proposed. 
 
The objective is to establish a room numbering system so that building occupants, users, visitors and staff are 
guided in a logical and sequential manner to rooms required. It is also necessary to incorporate each room number 
into the system database which holds detail for all space on the project / framework. 
 
Room numbering for all useable space is unique for each floor of a building. It is not possible to have a duplicate 
number allocated for more than one space on the same floor. Each identifiable space has its own unique number 
which is incorporated into the system database as  a room record. This room record will be linked to the 
corresponding room on an electronic drawing of the building. The room numbering for all useable space will 
commence from the main entrance of the building, or major point of access t o the floor, and be allocated in a 
clockwise direction, starting with the lowest number. Occupants/  visitors to the building should be able to follow 
the sequence of room numbers regardless of their point of entry to the building. The Design team should propose 
a standard room numbering procedure and describe this within the execution plan to be agreed with the client 
project and asset teams. 
 
• Circulation space and service space (building common areas) are numbered in the same way as occupied 
rooms. The numbering sequence for circulation space and service space can be repeated for each floor 
of a building. 
 
A change in corridor number will occur when there is a physical break e.g. doors, stairs etc. 
• Open plan areas are likely to become more common and may occur within large circulation spaces. Within 
these open plan areas, space usage may vary without the use of physical barriers in between them. Each 
of these spaces may be occupied and will require a unique room nu mber for database purposes and 
space charging purposes. The room numbering should follow the logical sequence already used for the 
useable space, and the circulation space should follow the logical sequence already used  within the 
building.