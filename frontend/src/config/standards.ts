import type { StandardSelection } from '../types/standards';

export const STANDARDS_OPTIONS: StandardSelection[] = [
	{
		standardId: 'BS EN ISO 19650-0:2018',
		title: 'Organization and digitization of information about buildings and civil engineering works — Information management using BIM — Part 0: Guidance',
		publicationDate: '2018',
		issuingOrganization: 'BSI / ISO',
		priority: 1,
		mandatory: true,
	},
	{
		standardId: 'BS EN ISO 19650-1:2018',
		title: 'Organization and digitization of information about buildings and civil engineering works — Information management using BIM — Part 1: Concepts and principles',
		publicationDate: '2018',
		issuingOrganization: 'BSI / ISO',
		priority: 2,
		mandatory: true,
	},
	{
		standardId: 'BS EN ISO 19650-2:2018 ISO 19650-2:2018(E)',
		title: 'Organization and digitization of information about buildings and civil engineering works — Information management using BIM — Part 2: Delivery phase of assets',
		publicationDate: '2018',
		issuingOrganization: 'BSI / ISO',
		priority: 3,
		mandatory: true,
	},
];

export const DEFAULT_SELECTED_STANDARDS = STANDARDS_OPTIONS;
