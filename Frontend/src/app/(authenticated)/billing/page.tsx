"use client";

import { useState, useEffect } from "react";
import { useSearchParams } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import { toast } from "sonner";
import { CheckCircle, FlaskConical, ExternalLink, Loader2 } from "lucide-react";
import { Header } from "@/components/layout/header";
import {
  Card,
  CardHeader,
  CardTitle,
  CardContent,
  CardFooter,
  CardAction,
  CardDescription,
} from "@/components/ui/card";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { Alert, AlertTitle, AlertDescription } from "@/components/ui/alert";
import { useTokenTransactions } from "@/hooks/use-tokens";
import { useInvoices, useCreatePortalSession } from "@/hooks/use-billing";
import { TopUpDialog } from "@/components/tokens/top-up-dialog";
import { queryKeys } from "@/lib/query-keys";
import { getTokenBalance } from "@/lib/api-client";
import { formatDate, formatCurrency } from "@/lib/format";

function transactionTypeBadge(type: string) {
  switch (type) {
    case "Purchase":
      return <Badge variant="default">Kauf</Badge>;
    case "Refund":
      return (
        <Badge
          variant="destructive"
          className="border border-destructive/50 bg-transparent text-destructive"
        >
          Rückerstattung
        </Badge>
      );
    case "Consumption":
      return <Badge variant="secondary">Verbrauch</Badge>;
    case "Adjustment":
      return <Badge variant="secondary">Anpassung</Badge>;
    default:
      return <Badge variant="secondary">{type}</Badge>;
  }
}

export default function BillingPage() {
  const searchParams = useSearchParams();
  const isDemoMode = searchParams.get("demo") === "true";
  // ?payment=success is appended by Stripe success_url (D-04)
  const isPaymentSuccess = searchParams.get("payment") === "success";

  const [isPolling, setIsPolling] = useState(false);
  const [topUpOpen, setTopUpOpen] = useState(false);

  useEffect(() => {
    if (isPaymentSuccess) {
      setIsPolling(true);
      const timeout = setTimeout(() => setIsPolling(false), 15_000);
      return () => clearTimeout(timeout);
    }
  }, [isPaymentSuccess]);

  const { data: tokenBalance, isLoading: balanceLoading } = useQuery({
    queryKey: queryKeys.tokens.balance,
    queryFn: getTokenBalance,
    refetchInterval: isPolling ? 3000 : 30_000,
  });

  const { data: transactions, isLoading: transactionsLoading } =
    useTokenTransactions(20);

  const { data: invoices, isLoading: invoicesLoading } = useInvoices();

  const createPortalSession = useCreatePortalSession();

  const handlePortal = async () => {
    try {
      const data = await createPortalSession.mutateAsync();
      window.location.href = data.url;
    } catch {
      toast.error("Weiterleitung fehlgeschlagen. Bitte versuchen Sie es erneut.");
    }
  };

  const balance = tokenBalance?.balance ?? 0;
  const isNegative = balance < 0;

  return (
    <>
      <Header title="Credits & Abrechnung" />
      <div className="flex-1 p-6 overflow-auto">
        <div className="mx-auto w-full max-w-4xl space-y-6">
          {/* DemoMode banner */}
          {isDemoMode && (
            <Alert
              role="alert"
              className="border-amber-500/30 bg-amber-50 text-amber-700 dark:bg-amber-500/10 dark:text-amber-400"
            >
              <FlaskConical className="h-4 w-4" />
              <AlertTitle>Demo-Modus</AlertTitle>
              <AlertDescription>
                Demo-Modus — keine echten Zahlungen. Credits werden direkt gutgeschrieben.
              </AlertDescription>
            </Alert>
          )}

          {/* Payment success banner */}
          {isPaymentSuccess && (
            <Alert className="border-primary/30 bg-primary/5">
              <CheckCircle className="h-4 w-4 text-primary" />
              <AlertTitle>Zahlung erhalten</AlertTitle>
              <AlertDescription>
                Ihre Credits werden in Kürze gutgeschrieben. Dies kann bis zu 30 Sekunden dauern.
              </AlertDescription>
            </Alert>
          )}

          <h1 className="text-2xl font-semibold">Credits & Abrechnung</h1>

          {/* Balance + Payment Method row */}
          <div className="grid md:grid-cols-2 gap-6">
            {/* Balance Card */}
            <Card>
              <CardHeader>
                <CardTitle>Guthaben</CardTitle>
                <CardAction>
                  <Button variant="default" size="sm" onClick={() => setTopUpOpen(true)}>
                    Credits aufladen
                  </Button>
                </CardAction>
              </CardHeader>
              <CardContent>
                {balanceLoading ? (
                  <Skeleton className="h-9 w-24" />
                ) : (
                  <div>
                    <p
                      className={
                        isNegative
                          ? "text-3xl font-semibold text-destructive"
                          : "text-3xl font-semibold"
                      }
                      aria-label={`${balance} Credits Guthaben`}
                    >
                      {balance} Credits
                      {isNegative && (
                        <span className="text-sm text-destructive ml-2">
                          (Konto gesperrt)
                        </span>
                      )}
                    </p>
                  </div>
                )}
              </CardContent>
              {isNegative && (
                <CardFooter>
                  <p className="text-sm text-destructive" role="status">
                    Neue Uploads sind gesperrt. Bitte laden Sie Credits auf, um fortzufahren.
                  </p>
                </CardFooter>
              )}
            </Card>

            {/* Payment Method Card */}
            <Card>
              <CardHeader>
                <CardTitle>Zahlungsmethode</CardTitle>
                <CardDescription>
                  Zahlungsmethoden verwalten, gespeicherte Karten ändern oder Zahlungshistorie einsehen.
                </CardDescription>
              </CardHeader>
              <CardFooter>
                <Button
                  variant="outline"
                  onClick={handlePortal}
                  disabled={createPortalSession.isPending}
                >
                  {createPortalSession.isPending ? (
                    <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                  ) : (
                    <ExternalLink className="mr-2 h-4 w-4" />
                  )}
                  Zahlungsmethode verwalten
                </Button>
              </CardFooter>
            </Card>
          </div>

          {/* Transaction History */}
          <Card>
            <CardHeader>
              <CardTitle>Transaktionsverlauf</CardTitle>
            </CardHeader>
            <CardContent>
              {transactionsLoading ? (
                <div className="space-y-2">
                  {Array.from({ length: 5 }).map((_, i) => (
                    <Skeleton key={i} className="h-10 w-full" />
                  ))}
                </div>
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Datum</TableHead>
                      <TableHead>Beschreibung</TableHead>
                      <TableHead>Credits</TableHead>
                      <TableHead>Typ</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {!transactions || transactions.length === 0 ? (
                      <TableRow>
                        <TableCell
                          colSpan={4}
                          className="text-sm text-muted-foreground text-center py-8"
                        >
                          Noch keine Transaktionen vorhanden.
                        </TableCell>
                      </TableRow>
                    ) : (
                      transactions.map((tx) => (
                        <TableRow key={tx.id}>
                          <TableCell>{formatDate(tx.createdAt)}</TableCell>
                          <TableCell>{tx.description}</TableCell>
                          <TableCell>
                            {tx.amount >= 0 ? (
                              <span className="text-primary">+{tx.amount}</span>
                            ) : (
                              <span className="text-destructive">
                                -{Math.abs(tx.amount)}
                              </span>
                            )}
                          </TableCell>
                          <TableCell>{transactionTypeBadge(tx.type)}</TableCell>
                        </TableRow>
                      ))
                    )}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>

          {/* Invoice List */}
          <Card>
            <CardHeader>
              <CardTitle>Rechnungen</CardTitle>
            </CardHeader>
            <CardContent>
              {invoicesLoading ? (
                <div className="space-y-2">
                  {Array.from({ length: 3 }).map((_, i) => (
                    <Skeleton key={i} className="h-10 w-full" />
                  ))}
                </div>
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Rechnungsnummer</TableHead>
                      <TableHead>Datum</TableHead>
                      <TableHead>Betrag</TableHead>
                      <TableHead>Aktionen</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {!invoices || invoices.length === 0 ? (
                      <TableRow>
                        <TableCell
                          colSpan={4}
                          className="text-sm text-muted-foreground text-center py-8"
                        >
                          Noch keine Rechnungen vorhanden.
                        </TableCell>
                      </TableRow>
                    ) : (
                      invoices.map((invoice) => (
                        <TableRow key={invoice.id}>
                          <TableCell>
                            {invoice.number ?? "—"}
                          </TableCell>
                          <TableCell>{formatDate(invoice.created)}</TableCell>
                          <TableCell>
                            {formatCurrency(invoice.amountPaid)}
                          </TableCell>
                          <TableCell>
                            {invoice.invoicePdfUrl ? (
                              <a
                                href={invoice.invoicePdfUrl}
                                target="_blank"
                                rel="noopener noreferrer"
                                className="inline-flex h-7 items-center justify-center rounded-[min(var(--radius-md),12px)] border border-border bg-background px-2.5 text-[0.8rem] font-medium text-foreground hover:bg-muted transition-colors"
                              >
                                PDF herunterladen
                              </a>
                            ) : (
                              <span className="text-sm text-muted-foreground">
                                Ausstehend
                              </span>
                            )}
                          </TableCell>
                        </TableRow>
                      ))
                    )}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>
        </div>
      </div>

      <TopUpDialog open={topUpOpen} onOpenChange={setTopUpOpen} />
    </>
  );
}
