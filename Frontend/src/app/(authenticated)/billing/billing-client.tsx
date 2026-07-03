"use client";

import { useState, useEffect } from "react";
import { useSearchParams } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import { toast } from "sonner";
import { CheckCircle, FlaskConical, ExternalLink, Loader2 } from "lucide-react";
import { cn } from "@/lib/utils";
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

// WR-03: credit tiers mirror TopUpDialog's PACKAGES / the backend allow-list in
// PaymentEndpoints.cs (validCredits). Keep in sync when adding/removing a tier.
const CREDIT_PACKS = [
  { credits: 100, price: 4.99, popular: false },
  { credits: 500, price: 19.99, popular: true },
  { credits: 1500, price: 49.99, popular: false },
];

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

export function BillingClient() {
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
  const isLowBalance = !balanceLoading && !isNegative && balance <= 3;
  const isZeroBalance = !balanceLoading && !isNegative && balance === 0;

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

          {/* Low/zero-credits banner (D-11) */}
          {isZeroBalance ? (
            <Alert variant="destructive">
              <AlertTitle>Keine Credits mehr</AlertTitle>
              <AlertDescription>
                Kaufen Sie Credits, um Belege klassifizieren zu können.
              </AlertDescription>
            </Alert>
          ) : isLowBalance ? (
            <Alert className="border-amber-200 bg-amber-50 text-amber-800 [&>svg]:text-amber-600 dark:border-amber-500/30 dark:bg-amber-500/10 dark:text-amber-400">
              <AlertTitle>Ihr Guthaben wird knapp</AlertTitle>
              <AlertDescription>
                Jetzt Credits kaufen.
              </AlertDescription>
            </Alert>
          ) : null}

          {/* Balance + Payment Method row */}
          <div className="grid md:grid-cols-2 gap-6">
            {/* Balance Card */}
            <Card>
              <CardHeader>
                <CardTitle>Guthaben</CardTitle>
                <CardAction>
                  <Button variant="default" size="sm" onClick={() => setTopUpOpen(true)}>
                    Credits kaufen
                  </Button>
                </CardAction>
              </CardHeader>
              <CardContent>
                {balanceLoading ? (
                  <Skeleton className="h-9 w-24" />
                ) : (
                  <div>
                    <p className="text-sm text-muted-foreground mb-1">
                      Ihr aktuelles Guthaben
                    </p>
                    <p
                      className={cn(
                        "text-3xl font-semibold",
                        isNegative && "text-destructive",
                      )}
                      aria-label={`Ihr aktuelles Guthaben: ${balance} Credits`}
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
                  Rechnungshistorie & Rechnungen anzeigen
                </Button>
              </CardFooter>
            </Card>
          </div>

          {/* Credit pack cards */}
          <div className="space-y-3">
            <h2 className="text-lg font-semibold">Credits kaufen</h2>
            <div className="grid gap-4 sm:grid-cols-3">
              {CREDIT_PACKS.map((pack) => (
                <Card
                  key={pack.credits}
                  className={pack.popular ? "border-primary" : undefined}
                >
                  <CardHeader>
                    <CardTitle className="flex items-center gap-2">
                      {pack.credits.toLocaleString("de-DE")} Credits
                      {pack.popular && (
                        <span className="text-xs font-normal rounded-full bg-secondary text-secondary-foreground px-2 py-0.5">
                          Beliebt
                        </span>
                      )}
                    </CardTitle>
                    <CardDescription>{formatCurrency(pack.price)}</CardDescription>
                  </CardHeader>
                  <CardFooter>
                    <Button
                      className="w-full"
                      onClick={() => setTopUpOpen(true)}
                      aria-label={`${pack.credits.toLocaleString("de-DE")} Credits für ${formatCurrency(pack.price)} kaufen`}
                    >
                      Credits kaufen
                    </Button>
                  </CardFooter>
                </Card>
              ))}
            </div>
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
              ) : !transactions || transactions.length === 0 ? (
                <p className="text-sm text-muted-foreground text-center py-8">
                  Noch keine Transaktionen vorhanden.
                </p>
              ) : (
                <>
                  {/* Mobile: card list */}
                  <div className="md:hidden divide-y divide-border">
                    {transactions.map((tx) => (
                      <div key={tx.id} className="py-3 first:pt-0 last:pb-0">
                        <div className="flex items-start justify-between gap-2">
                          <span className="text-sm font-medium">{tx.description}</span>
                          {transactionTypeBadge(tx.type)}
                        </div>
                        <div className="mt-1 flex items-center justify-between gap-2 text-sm text-muted-foreground">
                          <span>{formatDate(tx.createdAt)}</span>
                          {tx.amount >= 0 ? (
                            <span className="text-primary font-medium">+{tx.amount} Credits</span>
                          ) : (
                            <span className="text-destructive font-medium">
                              -{Math.abs(tx.amount)} Credits
                            </span>
                          )}
                        </div>
                      </div>
                    ))}
                  </div>

                  {/* Desktop: table */}
                  <div className="hidden md:block">
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
                        {transactions.map((tx) => (
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
                        ))}
                      </TableBody>
                    </Table>
                  </div>
                </>
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
              ) : !invoices || invoices.length === 0 ? (
                <p className="text-sm text-muted-foreground text-center py-8">
                  Noch keine Rechnungen vorhanden.
                </p>
              ) : (
                <>
                  {/* Mobile: card list */}
                  <div className="md:hidden divide-y divide-border">
                    {invoices.map((invoice) => (
                      <div key={invoice.id} className="py-3 first:pt-0 last:pb-0">
                        <div className="flex items-start justify-between gap-2">
                          <span className="text-sm font-medium">
                            {invoice.number ?? "—"}
                          </span>
                          <span className="text-sm font-medium">
                            {formatCurrency(invoice.amountPaid)}
                          </span>
                        </div>
                        <div className="mt-1 flex items-center justify-between gap-2 text-sm text-muted-foreground">
                          <span>{formatDate(invoice.created)}</span>
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
                            <span>Ausstehend</span>
                          )}
                        </div>
                      </div>
                    ))}
                  </div>

                  {/* Desktop: table */}
                  <div className="hidden md:block">
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
                        {invoices.map((invoice) => (
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
                        ))}
                      </TableBody>
                    </Table>
                  </div>
                </>
              )}
            </CardContent>
          </Card>
        </div>
      </div>

      <TopUpDialog open={topUpOpen} onOpenChange={setTopUpOpen} />
    </>
  );
}
