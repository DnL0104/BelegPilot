import type { Metadata } from "next";
import { RegisterClient } from "./register-client";

export const metadata: Metadata = {
  title: "Registrieren | BelegPilot",
};

export default function RegisterPage() {
  return <RegisterClient />;
}
