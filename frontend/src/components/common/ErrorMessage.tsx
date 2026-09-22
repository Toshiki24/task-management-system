export function ErrorMessage({ message }: { message: string }) {
  return (
    <p role="alert" className="text-sm text-red-600">
      {message}
    </p>
  );
}
