import { PrescriptionSummary } from '../prescriptions/prescription.models';

export interface Patient {
  id: number;
  fullName: string;
  dateOfBirth: string;
  age: number;
  phone: string | null;
  address: string | null;
  allergies: string | null;
  notes: string | null;
  consentGivenAt: string;
  createdAt: string;
}

export interface PatientDetail extends Patient {
  prescriptions: PrescriptionSummary[];
}

export interface PatientRequest {
  fullName: string;
  dateOfBirth: string;
  phone: string | null;
  address: string | null;
  allergies: string | null;
  notes: string | null;
  consentGiven: boolean;
}
